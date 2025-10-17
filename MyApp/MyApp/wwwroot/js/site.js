// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", () => {
    const menuContainers = document.querySelectorAll("[data-profile-menu-container]");

    if (menuContainers.length === 0) {
        return;
    }

    const closeMenus = () => {
        menuContainers.forEach((container) => {
            const menu = container.querySelector("[data-profile-menu]");
            const trigger = container.querySelector("[data-profile-menu-trigger]");

            if (menu !== null) {
                menu.classList.add("hidden");
            }

            if (trigger !== null) {
                trigger.setAttribute("aria-expanded", "false");
            }
        });
    };

    menuContainers.forEach((container) => {
        const menu = container.querySelector("[data-profile-menu]");
        const trigger = container.querySelector("[data-profile-menu-trigger]");

        if (menu === null || trigger === null) {
            return;
        }

        trigger.addEventListener("click", (event) => {
            event.preventDefault();

            const isHidden = menu.classList.contains("hidden");

            closeMenus();

            if (isHidden === true) {
                menu.classList.remove("hidden");
                trigger.setAttribute("aria-expanded", "true");
            }
        });
    });

    document.addEventListener("click", (event) => {
        if (event.target instanceof Element === false) {
            return;
        }

        const containerMatch = event.target.closest("[data-profile-menu-container]");

        if (containerMatch === null) {
            closeMenus();
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            closeMenus();
        }
    });
});
