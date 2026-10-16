// Scrolls the booking grid to the afternoon and taps the first free slot that has room for an hour, which opens the buy dialog for members who are not enrolled.
new Promise(resolve => {
    const container = document.querySelector('.day-scroll-container');
    container.scrollIntoView({ block: 'start' });
    container.scrollTop = 420;
    setTimeout(() => {
        const box = container.getBoundingClientRect();
        const bottom = Math.min(box.bottom, window.innerHeight);
        const isSlot = (x, y) => document.elementFromPoint(x, y)?.closest('.day__slot') != null;
        const free = [...document.querySelectorAll('.day__slot')].find(slot => {
            const r = slot.getBoundingClientRect();
            if (r.top < box.top + 40 || r.top + r.height * 2 > bottom || r.left < box.left || r.right > Math.min(box.right, window.innerWidth)) return false;
            const x = r.left + r.width / 2;
            return isSlot(x, r.top + r.height / 2) && isSlot(x, r.top + r.height * 1.5);
        });
        if (free) free.click();
        resolve(!!free);
    }, 800);
})
