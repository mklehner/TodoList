// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function sendStatusAjax(element) {
    const id = element.getAttribute("data-id");
    const newValue = element.value;
    const oldValue = element.getAttribute("data-old-value");

    if (newValue === oldValue) return;

    fetch('/Bewerbung/UpdateStatus', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ 'id': id, 'status': newValue })
    })
    .then(response => {
        if (response.ok) {
            element.setAttribute("data-old-value", newValue);
            element.parentElement.classList.add("border-success");
            setTimeout(() => element.parentElement.classList.remove("border-success"), 1000);
        } else {
            alert("Fehler beim Speichern des Status.");
            element.value = oldValue;
        }
    })
    .catch(error => {
        console.error("Fehler:", error);
        element.value = oldValue;
    });
}

function sendBatchIdAjax(element) {
    const id = element.getAttribute("data-id");
    const newValue = element.value.trim();
    const oldValue = element.getAttribute("data-old-value");

    if (newValue === oldValue) return;
    if (newValue === "" || parseInt(newValue) < 0) {
        alert("Bitte eine gültige Batch-Nummer eingeben.");
        element.value = oldValue;
        return;
    }

    fetch('/Bewerbung/UpdateBatchId', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ 'id': id, 'batchId': newValue })
    })
    .then(response => {
        if (response.ok) {
            element.setAttribute("data-old-value", newValue);
            element.parentElement.classList.add("border-success");
            setTimeout(() => element.parentElement.classList.remove("border-success"), 1000);
        } else {
            alert("Fehler beim Speichern der Batch-ID.");
            element.value = oldValue;
        }
    })
    .catch(error => {
        console.error("Fehler:", error);
        element.value = oldValue;
    });
}
