/*
 * =========================================================
 * SUBMIT FEEDBACK
 * =========================================================
 *
 * For any form marked  data-resk-submit  this script:
 *
 *   - checks the confirmation box (data-resk-confirm="#id")
 *     is ticked and says so if it isn't,
 *   - disables the submit button and changes its text
 *     (data-busy-text) so it can't be clicked twice,
 *   - shows a status message (data-resk-status="#id") that
 *     screen readers announce (role="status").
 *
 * "Save draft" submits the form from code, which doesn't
 * fire the submit event, so it isn't affected.
 */
(function () {
    "use strict";

    function setStatus(status, text, kind) {
        if (!status) {
            return;
        }

        status.textContent = text;
        status.classList.remove("is-error", "is-success");

        if (kind) {
            status.classList.add("is-" + kind);
        }
    }

    function wire(form) {
        var button = form.querySelector("button[type=submit]");
        var status = document.querySelector(form.getAttribute("data-resk-status"));
        var confirmBox = document.querySelector(form.getAttribute("data-resk-confirm"));

        form.addEventListener("submit", function (event) {
            if (confirmBox && !confirmBox.checked) {
                event.preventDefault();
                setStatus(status, "Tick the confirmation box above before submitting.", "error");
                confirmBox.focus();
                return;
            }

            if (form.getAttribute("data-submitting") === "true") {
                event.preventDefault();
                return;
            }

            form.setAttribute("data-submitting", "true");

            if (button) {
                button.disabled = true;
                button.classList.add("is-busy");
                button.setAttribute("aria-busy", "true");
                button.textContent = button.getAttribute("data-busy-text") || "Submitting…";
            }

            setStatus(status, form.getAttribute("data-busy-message") || "Submitting… please wait.", null);
        });

        if (confirmBox) {
            confirmBox.addEventListener("change", function () {
                if (confirmBox.checked) {
                    setStatus(status, "", null);
                }
            });
        }
    }

    // Coming back with the browser Back button: undo the "busy" state.
    window.addEventListener("pageshow", function (event) {
        if (!event.persisted) {
            return;
        }

        document.querySelectorAll("form[data-resk-submit]").forEach(function (form) {
            form.removeAttribute("data-submitting");

            var button = form.querySelector("button[type=submit]");

            if (button) {
                button.disabled = false;
                button.classList.remove("is-busy");
                button.removeAttribute("aria-busy");
                button.textContent = button.getAttribute("data-idle-text") || button.textContent;
            }
        });
    });

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("form[data-resk-submit]").forEach(function (form) {
            var button = form.querySelector("button[type=submit]");

            if (button) {
                button.setAttribute("data-idle-text", button.textContent.trim());
            }

            wire(form);
        });
    });
})();
