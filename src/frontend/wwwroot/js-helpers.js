var App = App || {};
App.getWidth = function (element) {
  return element.getBoundingClientRect().width;
}

App.downloadFileFromStream = async function (fileName, contentStreamReference) {
  const arrayBuffer = await contentStreamReference.arrayBuffer();
  const url = URL.createObjectURL(new Blob([arrayBuffer]));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName ?? '';
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

// Scrolls a container so that the child sits in the upper third, or to the top without a child
App.scrollToChild = function (container, child) {
  if (!container) {
    return;
  }
  const top = child ? Math.max(child.offsetTop - container.clientHeight / 3, 0) : 0;
  container.scrollTo({ top: top, behavior: 'smooth' });
}

// Scrolls the nearest scrollable parent (e.g. a dialog) so that the element is visible
App.scrollIntoView = function (element) {
  if (element) {
    element.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }
}

// Inserts text at the cursor of the input/textarea inside the container and returns the new value
App.insertAtCursor = function (container, text) {
  const input = container ? container.querySelector('textarea, input') : null;
  if (!input) {
    return null;
  }
  const start = input.selectionStart ?? input.value.length;
  const end = input.selectionEnd ?? input.value.length;
  const value = input.value.slice(0, start) + text + input.value.slice(end);
  input.value = value;
  input.focus();
  input.setSelectionRange(start + text.length, start + text.length);
  return value;
}

// Opens the native share sheet (phones) or copies the url to the clipboard.
// Returns 'shared', 'copied' or 'cancelled'.
App.shareOrCopy = async function (title, text, url) {
  if (navigator.share) {
    try {
      await navigator.share({ title: title, text: text, url: url });
      return 'shared';
    } catch (e) {
      if (e && e.name === 'AbortError') {
        return 'cancelled';
      }
      // Sharing not possible (e.g. not allowed in this context), fall back to the clipboard
    }
  }
  await navigator.clipboard.writeText(url);
  return 'copied';
}

App.copyToClipboard = async function (text) {
  await navigator.clipboard.writeText(text);
}

window.ChangeUrl = function (url) {
  history.pushState(null, '', url);
}


// Goes back in the browser history. Returns false if there is nothing to go back to (e.g. the page was opened in a new tab).
App.goBack = function () {
  if (window.history.length > 1) {
    window.history.back();
    return true;
  }
  return false;
}
