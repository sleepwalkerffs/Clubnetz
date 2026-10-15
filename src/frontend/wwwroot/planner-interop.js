// Pointer interaction of the multi-day booking planner (Pages/ClubShell/Planner).
// Moving and resizing bookings and selecting regions runs entirely in the browser (60fps, no .NET round trip per pixel);
// .NET renders the grid, sends the geometry and the occupied times with update(data) and gets the results:
//   OnBookingDropped(bookingId, courtId, fromMs, toMs) -> bool   a booking was moved or resized (true = saved)
//   OnBookingClicked(bookingId)                                   a booking was clicked without dragging it
//   OnSelectionChanged([{ courtStart, courtEnd, from, to }])       the selected regions changed
// The pre-checks here only give feedback while dragging; the server validates everything again.
// There is one planner on a page; init returns an id that every later call passes, so a planner that is being disposed
// (e.g. after a fast navigation) can't change or remove the state of the planner that replaced it.

const ZOOM_KEY = 'bookennis-planner-zoom';
const DRAG_THRESHOLD = 4;
const RESIZE_EDGE = 8;
const AUTOSCROLL_EDGE = 48;
const AUTOSCROLL_SPEED = 14;

let state = null;
let lastId = 0;

export function loadZoom() {
    try {
        const value = parseInt(localStorage.getItem(ZOOM_KEY), 10);
        return Number.isFinite(value) ? value : 0;
    } catch {
        return 0;
    }
}

export function saveZoom(value) {
    try {
        localStorage.setItem(ZOOM_KEY, String(value));
    } catch {
        // Storage can be blocked (private mode); the zoom is then only kept for this visit
    }
}

export function init(lanes, scroller, dotNet) {
    if (state) {
        dispose(state.id);
    }
    state = {
        id: ++lastId,
        lanes,
        scroller,
        dotNet,
        overlay: lanes.querySelector('.planner-overlay'),
        data: null,
        bookings: new Map(),
        regions: [],
        regionElements: [],
        drag: null,
        busy: false,
        lastPointer: null,
        scrollFrame: 0,
        tip: null,
    };
    lanes.addEventListener('pointerdown', onPointerDown);
    lanes.addEventListener('pointermove', onPointerMove);
    lanes.addEventListener('pointerup', onPointerUp);
    lanes.addEventListener('pointercancel', onPointerCancel);
    lanes.addEventListener('lostpointercapture', onPointerCancel);
    lanes.addEventListener('keydown', onBlockKeyDown);
    window.addEventListener('keydown', onKeyDown);
    return state.id;
}

function isCurrent(id) {
    return state !== null && state.id === id;
}

export function update(id, data) {
    if (!isCurrent(id)) {
        return;
    }
    state.data = data;
    state.bookings = new Map(data.bookings.map(b => [b.id, b]));
    renderRegions();
}

export function clearSelection(id) {
    if (!isCurrent(id)) {
        return;
    }
    state.regions = [];
    renderRegions();
}

export function dispose(id) {
    if (!isCurrent(id)) {
        return;
    }
    cancelDrag();
    hideTip();
    const { lanes } = state;
    lanes.removeEventListener('pointerdown', onPointerDown);
    lanes.removeEventListener('pointermove', onPointerMove);
    lanes.removeEventListener('pointerup', onPointerUp);
    lanes.removeEventListener('pointercancel', onPointerCancel);
    lanes.removeEventListener('lostpointercapture', onPointerCancel);
    lanes.removeEventListener('keydown', onBlockKeyDown);
    window.removeEventListener('keydown', onKeyDown);
    state.regionElements.forEach(el => el.remove());
    state = null;
}

// ---------- Geometry ----------

function slotMs() {
    return state.data.slotMinutes * 60000;
}

// Every day has its own opening time and number of slots (daylight saving time, half-hour opening hours):
// days = [{ open, close, slots, left }] with times in Unix milliseconds and left in pixels.
function day(index) {
    return state.data.days[index];
}

function dayWidth(index) {
    return day(index).slots * state.data.slotWidth;
}

function dayOpen(index) {
    return day(index).open;
}

function dayClose(index) {
    return day(index).close;
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

function dayOfTime(ms) {
    const days = state.data.days;
    for (let i = 0; i < days.length; i++) {
        if (ms >= days[i].open && ms < days[i].close) {
            return i;
        }
    }
    return -1;
}

function dayAtX(x) {
    const days = state.data.days;
    for (let i = days.length - 1; i >= 0; i--) {
        if (x >= days[i].left) {
            return i;
        }
    }
    return 0;
}

function xOfTime(index, ms) {
    return day(index).left + (ms - dayOpen(index)) / slotMs() * state.data.slotWidth;
}

function localPoint(event) {
    const rect = state.lanes.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
}

function courtAt(y) {
    return clamp(Math.floor(y / state.data.rowHeight), 0, state.data.courts.length - 1);
}

function cellAt(x, fixedDay) {
    const index = fixedDay ?? dayAtX(x);
    const slot = clamp(Math.floor((x - day(index).left) / state.data.slotWidth), 0, day(index).slots - 1);
    return { day: index, slot };
}

function pad(value) {
    return String(value).padStart(2, '0');
}

function formatTime(ms) {
    const date = new Date(ms);
    return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function formatRange(from, to) {
    return `${formatTime(from)}–${formatTime(to)}`;
}

// ---------- Rules (pre-check only, the server decides) ----------

// Returns { status: 'ok' | 'overbook' | 'invalid', reason }
function check(courtIndex, from, to, bookingId, canOverbook) {
    const data = state.data;
    if (!data.allowPast && from < data.now) {
        return { status: 'invalid', reason: data.texts.past };
    }

    let overbook = false;
    for (const item of data.courts[courtIndex].occupied) {
        if (item.from >= to || item.to <= from) {
            continue;
        }
        if (item.kind === 'blocked') {
            return { status: 'invalid', reason: data.texts.blocked };
        }
        if (item.kind === 'inactive') {
            return { status: 'invalid', reason: data.texts.inactive };
        }
        if (item.bookingId === bookingId) {
            continue;
        }
        if (!canOverbook) {
            return { status: 'invalid', reason: data.texts.conflict };
        }
        overbook = true;
    }
    return overbook ? { status: 'overbook', reason: data.texts.overbook } : { status: 'ok', reason: '' };
}

function checkRegion(region) {
    const data = state.data;
    for (let court = region.courtStart; court <= region.courtEnd; court++) {
        if (!data.allowPast && region.from < data.now) {
            return { status: 'invalid', reason: data.texts.past };
        }
        for (const item of data.courts[court].occupied) {
            if (item.from >= region.to || item.to <= region.from) {
                continue;
            }
            if (item.kind === 'blocked') {
                return { status: 'invalid', reason: data.texts.blocked };
            }
            if (item.kind === 'inactive') {
                return { status: 'invalid', reason: data.texts.inactive };
            }
        }
    }
    const overlaps = state.regions.some(other => other !== region
        && other.from < region.to && other.to > region.from
        && other.courtStart <= region.courtEnd && other.courtEnd >= region.courtStart);
    if (overlaps) {
        return { status: 'invalid', reason: data.texts.overlap };
    }
    return { status: 'ok', reason: '' };
}

// ---------- Pointer handling ----------

function onPointerDown(event) {
    if (!state || !state.data || event.button !== 0 || state.drag) {
        return;
    }

    const block = event.target.closest('.planner-block');
    const additive = event.ctrlKey || event.metaKey;
    const point = localPoint(event);

    if (block && !additive) {
        const booking = state.bookings.get(Number(block.dataset.bookingId));
        if (!booking) {
            return;
        }
        // The edges resize (unless the play mode has a fixed duration); measured instead of hit-testing the handles,
        // because the colored border of the block is outside its content box
        let mode = 'move';
        if (booking.resizable && booking.draggable) {
            const rect = block.getBoundingClientRect();
            const edge = Math.min(RESIZE_EDGE, rect.width / 4);
            if (event.clientX - rect.left <= edge) {
                mode = 'start';
            } else if (rect.right - event.clientX <= edge) {
                mode = 'end';
            }
        }
        state.drag = {
            kind: mode === 'move' ? 'move' : 'resize',
            edge: mode,
            booking,
            block,
            pointerId: event.pointerId,
            startClientX: event.clientX,
            startClientY: event.clientY,
            grabOffset: point.x - xOfTime(booking.day, booking.from),
            day: booking.day,
            court: booking.court,
            from: booking.from,
            to: booking.to,
            result: { status: 'ok', reason: '' },
            moved: false,
            ghost: null,
        };
    } else {
        if (!state.data.canSelect || state.busy || state.data.busy) {
            return;
        }
        const cell = cellAt(point.x);
        state.drag = {
            kind: 'select',
            additive,
            pointerId: event.pointerId,
            startClientX: event.clientX,
            startClientY: event.clientY,
            day: cell.day,
            startSlot: cell.slot,
            startCourt: courtAt(point.y),
            region: null,
            result: { status: 'ok', reason: '' },
            moved: false,
            band: null,
        };
        updateSelection(point);
    }

    event.preventDefault();
    state.lastPointer = { clientX: event.clientX, clientY: event.clientY };
    try { state.lanes.setPointerCapture(event.pointerId); } catch { /* pointer already gone; the document listeners still end the drag */ }
}

// Enter and Space open a focused booking (Space must not scroll the page)
function onBlockKeyDown(event) {
    if (!state || (event.key !== 'Enter' && event.key !== ' ')) {
        return;
    }
    const block = event.target.closest('.planner-block');
    if (!block || block.classList.contains('planner-ghost')) {
        return;
    }
    event.preventDefault();
    state.dotNet.invokeMethodAsync('OnBookingClicked', Number(block.dataset.bookingId));
}

// ---------- Reason next to the pointer ----------

// The ghost can be a few pixels wide at the smallest zoom, so why a place is refused is shown next to the pointer
function showTip(text, kind) {
    if (!text || !state.lastPointer) {
        hideTip();
        return;
    }
    if (!state.tip) {
        state.tip = document.createElement('div');
        state.tip.setAttribute('role', 'status');
        // Inside the scroll container (scoped styles), position: fixed keeps it out of the clipped lanes
        state.scroller.appendChild(state.tip);
    }
    state.tip.className = `planner-tip${kind === 'overbook' ? ' planner-tip--overbook' : ''}`;
    state.tip.textContent = text;
    state.tip.style.left = `${state.lastPointer.clientX + 14}px`;
    state.tip.style.top = `${state.lastPointer.clientY + 18}px`;
}

function hideTip() {
    if (state?.tip) {
        state.tip.remove();
        state.tip = null;
    }
}

function onPointerMove(event) {
    const drag = state?.drag;
    if (!drag || event.pointerId !== drag.pointerId) {
        return;
    }
    state.lastPointer = { clientX: event.clientX, clientY: event.clientY };

    if (!drag.moved) {
        const distance = Math.hypot(event.clientX - drag.startClientX, event.clientY - drag.startClientY);
        if (distance < DRAG_THRESHOLD) {
            return;
        }
        drag.moved = true;
        if (drag.kind !== 'select' && (!drag.booking.draggable || state.busy || state.data.busy)) {
            // Not allowed to change this booking: the pointer only shows that (cursor), nothing moves
            return;
        }
    }

    handleMove();
    updateAutoScroll();
}

function handleMove() {
    const drag = state.drag;
    if (!drag || !state.lastPointer) {
        return;
    }
    const point = localPoint(state.lastPointer);
    if (drag.kind === 'select') {
        updateSelection(point);
    } else if (drag.booking.draggable && !state.busy && !state.data.busy) {
        updateGhost(point);
    }
}

async function onPointerUp(event) {
    const drag = state?.drag;
    if (!drag || event.pointerId !== drag.pointerId) {
        return;
    }
    stopAutoScroll();
    state.drag = null;
    if (state.lanes.hasPointerCapture(event.pointerId)) {
        state.lanes.releasePointerCapture(event.pointerId);
    }

    if (drag.kind === 'select') {
        finishSelection(drag);
        return;
    }

    if (!drag.moved) {
        state.dotNet.invokeMethodAsync('OnBookingClicked', drag.booking.id);
        return;
    }

    if (!drag.ghost) {
        return;
    }

    const booking = drag.booking;
    const unchanged = drag.court === booking.court && drag.from === booking.from && drag.to === booking.to;
    if (unchanged || drag.result.status === 'invalid') {
        removeGhost(drag);
        return;
    }

    // Keep the ghost at the new place until .NET saved (or refused) the change
    hideTip();
    state.busy = true;
    drag.ghost.classList.add('is-saving');
    try {
        const courtId = state.data.courts[drag.court].courtId;
        await state.dotNet.invokeMethodAsync('OnBookingDropped', booking.id, courtId, drag.from, drag.to);
    } catch (error) {
        console.error(error);
    } finally {
        removeGhost(drag);
        if (state) {
            state.busy = false;
        }
    }
}

function onPointerCancel(event) {
    const drag = state?.drag;
    if (!drag || event.pointerId !== drag.pointerId) {
        return;
    }
    cancelDrag();
}

function cancelDrag() {
    const drag = state?.drag;
    if (!drag) {
        return;
    }
    stopAutoScroll();
    state.drag = null;
    if (state.lanes.hasPointerCapture(drag.pointerId)) {
        state.lanes.releasePointerCapture(drag.pointerId);
    }
    hideTip();
    if (drag.kind === 'select') {
        drag.band?.remove();
    } else {
        removeGhost(drag);
    }
}

function onKeyDown(event) {
    if (!state || event.key !== 'Escape') {
        return;
    }
    if (state.drag) {
        cancelDrag();
        event.preventDefault();
        return;
    }
    // Escape in a dialog (e.g. the overbooking confirmation) belongs to the dialog
    if (document.querySelector('.mud-dialog')) {
        return;
    }
    if (state.regions.length > 0) {
        state.regions = [];
        renderRegions();
        notifySelection();
    }
}

// ---------- Moving and resizing ----------

function updateGhost(point) {
    const drag = state.drag;
    const booking = drag.booking;
    const data = state.data;
    const slot = slotMs();

    let day = booking.day;
    let court = booking.court;
    let from = booking.from;
    let to = booking.to;

    if (drag.kind === 'move') {
        const duration = booking.to - booking.from;
        const left = point.x - drag.grabOffset;
        day = dayAtX(left + data.slotWidth / 2);
        const maxSlot = Math.floor((dayClose(day) - dayOpen(day) - duration) / slot);
        const startSlot = clamp(Math.round((left - data.days[day].left) / data.slotWidth), 0, Math.max(maxSlot, 0));
        court = courtAt(point.y);
        from = dayOpen(day) + startSlot * slot;
        to = from + duration;
    } else {
        const edgeSlot = clamp(Math.round((point.x - data.days[day].left) / data.slotWidth), 0, data.days[day].slots);
        const time = Math.min(dayOpen(day) + edgeSlot * slot, dayClose(day));
        if (drag.edge === 'start') {
            from = clamp(time, dayOpen(day), booking.to - slot);
        } else {
            to = clamp(time, booking.from + slot, dayClose(day));
        }
    }

    drag.day = day;
    drag.court = court;
    drag.from = from;
    drag.to = to;
    drag.result = check(court, from, to, booking.id, booking.canOverbook);

    if (!drag.ghost) {
        const ghost = drag.block.cloneNode(true);
        ghost.removeAttribute('tabindex');
        ghost.removeAttribute('title');
        ghost.setAttribute('aria-hidden', 'true');
        ghost.classList.add('planner-ghost');
        state.overlay.appendChild(ghost);
        drag.ghost = ghost;
        drag.block.classList.add('is-dragging');
    }

    const ghost = drag.ghost;
    ghost.style.left = `${xOfTime(day, from)}px`;
    ghost.style.width = `${Math.max((to - from) / slot * data.slotWidth, 4)}px`;
    ghost.style.top = `${court * data.rowHeight}px`;
    ghost.classList.toggle('is-invalid', drag.result.status === 'invalid');
    ghost.classList.toggle('is-overbook', drag.result.status === 'overbook');
    ghost.classList.toggle('is-valid', drag.result.status === 'ok');
    showTip(drag.result.status === 'ok' ? '' : drag.result.reason, drag.result.status);
    const time = ghost.querySelector('.planner-block__time');
    if (time) {
        time.textContent = formatRange(from, to);
    }
}

function removeGhost(drag) {
    hideTip();
    drag.ghost?.remove();
    drag.ghost = null;
    drag.block?.classList.remove('is-dragging');
}

// ---------- Selecting regions ----------

function updateSelection(point) {
    const drag = state.drag;
    const cell = cellAt(point.x, drag.day);
    const court = courtAt(point.y);
    const slot = slotMs();

    const region = {
        courtStart: Math.min(drag.startCourt, court),
        courtEnd: Math.max(drag.startCourt, court),
        from: dayOpen(drag.day) + Math.min(drag.startSlot, cell.slot) * slot,
        to: Math.min(dayOpen(drag.day) + (Math.max(drag.startSlot, cell.slot) + 1) * slot, dayClose(drag.day)),
    };
    drag.region = region;

    // A plain drag replaces the selection, so only regions that are kept count as overlapping
    const kept = drag.additive ? state.regions : [];
    const previous = state.regions;
    state.regions = kept;
    drag.result = checkRegion(region);
    state.regions = previous;

    if (!drag.band) {
        drag.band = document.createElement('div');
        drag.band.className = 'planner-region planner-region--band';
        drag.band.setAttribute('aria-hidden', 'true');
        state.overlay.appendChild(drag.band);
    }
    placeRegion(drag.band, region);
    drag.band.classList.toggle('is-invalid', drag.result.status === 'invalid');
    showTip(drag.result.status === 'invalid' ? drag.result.reason : '', drag.result.status);
}

function finishSelection(drag) {
    hideTip();
    drag.band?.remove();
    const region = drag.region;
    if (!region) {
        return;
    }

    if (drag.additive && !drag.moved) {
        // Ctrl + click on a selected region removes it
        const hit = state.regions.find(r => r.from <= region.from && r.to >= region.to
            && r.courtStart <= region.courtStart && r.courtEnd >= region.courtEnd);
        if (hit) {
            state.regions = state.regions.filter(r => r !== hit);
            renderRegions();
            notifySelection();
            return;
        }
    }

    if (drag.result.status === 'invalid') {
        return;
    }

    state.regions = drag.additive ? [...state.regions, region] : [region];
    renderRegions();
    notifySelection();
}

function placeRegion(element, region) {
    const data = state.data;
    const day = dayOfTime(region.from);
    if (day < 0) {
        element.style.display = 'none';
        return;
    }
    element.style.display = '';
    element.style.left = `${xOfTime(day, region.from)}px`;
    element.style.width = `${(region.to - region.from) / slotMs() * data.slotWidth}px`;
    element.style.top = `${region.courtStart * data.rowHeight}px`;
    element.style.height = `${(region.courtEnd - region.courtStart + 1) * data.rowHeight}px`;
    element.dataset.label = formatRange(region.from, region.to);
}

function renderRegions() {
    if (!state || !state.data) {
        return;
    }
    state.regionElements.forEach(el => el.remove());
    // Regions of courts that are gone (e.g. after the courts changed) can't be shown or booked
    state.regions = state.regions.filter(r => r.courtEnd < state.data.courts.length);
    state.regionElements = state.regions.map(region => {
        const element = document.createElement('div');
        element.className = 'planner-region';
        element.setAttribute('aria-hidden', 'true');
        placeRegion(element, region);
        state.overlay.appendChild(element);
        return element;
    });
}

function notifySelection() {
    const regions = state.regions.map(r => ({ courtStart: r.courtStart, courtEnd: r.courtEnd, from: r.from, to: r.to }));
    state.dotNet.invokeMethodAsync('OnSelectionChanged', regions);
}

// ---------- Scrolling while dragging near the edges ----------

function updateAutoScroll() {
    if (!state.lastPointer || state.scrollFrame) {
        return;
    }
    const step = () => {
        state.scrollFrame = 0;
        if (!state?.drag || !state.lastPointer) {
            return;
        }
        const rect = state.scroller.getBoundingClientRect();
        const { clientX, clientY } = state.lastPointer;
        // The court names and the header are sticky, scrolling starts next to them
        const left = rect.left + state.data.nameColumnWidth;
        const top = rect.top + state.data.headerHeight;
        let dx = 0;
        let dy = 0;
        if (clientX < left + AUTOSCROLL_EDGE) dx = -AUTOSCROLL_SPEED;
        else if (clientX > rect.right - AUTOSCROLL_EDGE) dx = AUTOSCROLL_SPEED;
        if (clientY < top + AUTOSCROLL_EDGE) dy = -AUTOSCROLL_SPEED;
        else if (clientY > rect.bottom - AUTOSCROLL_EDGE) dy = AUTOSCROLL_SPEED;
        if (dx === 0 && dy === 0) {
            return;
        }
        const beforeX = state.scroller.scrollLeft;
        const beforeY = state.scroller.scrollTop;
        state.scroller.scrollLeft += dx;
        state.scroller.scrollTop += dy;
        if (state.scroller.scrollLeft === beforeX && state.scroller.scrollTop === beforeY) {
            return;
        }
        handleMove();
        state.scrollFrame = requestAnimationFrame(step);
    };
    state.scrollFrame = requestAnimationFrame(step);
}

function stopAutoScroll() {
    if (state?.scrollFrame) {
        cancelAnimationFrame(state.scrollFrame);
        state.scrollFrame = 0;
    }
    if (state) {
        state.lastPointer = null;
    }
}
