// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", () => {
    initializeProfileMenu();
    initializeResizableSidebar();
});

const initializeProfileMenu = () => {
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
};

const initializeResizableSidebar = () => {
    const layoutRoot = document.querySelector("[data-layout-root]");
    const sidebar = document.querySelector("[data-resizable-sidebar]");
    const handle = document.querySelector("[data-resize-handle]");

    if (layoutRoot === null || sidebar === null || handle === null) {
        return;
    }

    const storageKey = "layout:sidebar-width";
    const minWidth = 280;
    const maxViewportRatio = 0.6;
    const mediaQuery = window.matchMedia("(min-width: 1024px)");
    const backdrop = document.querySelector("[data-sidebar-backdrop]");
    const openButton = document.querySelector("[data-open-sidebar]");
    const closeButtons = document.querySelectorAll("[data-close-sidebar]");
    const body = document.body;
    const floatingTrigger = document.querySelector("[data-sidebar-floating-trigger]");

    const clampWidth = (width) => {
        const viewportWidth = window.innerWidth;
        const ratioWidth = viewportWidth * maxViewportRatio;
        const remainingWidth = viewportWidth - 240;
        const computedMax = Math.max(minWidth, Math.min(ratioWidth, remainingWidth));

        return Math.min(Math.max(width, minWidth), computedMax);
    };

    let currentWidth = clampWidth(320);
    let isDragging = false;
    let dragPointerId = null;
    let isCollapsed = false;
    let swipeStartX = null;

    try {
        const storedWidth = window.localStorage.getItem(storageKey);

        if (storedWidth !== null) {
            const parsedWidth = parseInt(storedWidth, 10);

            if (Number.isFinite(parsedWidth)) {
                currentWidth = clampWidth(parsedWidth);
            }
        }
    }
    catch (error) {
        // Ignored on purpose when localStorage is unavailable.
    }

    const applyWidth = () => {
        if (mediaQuery.matches === false || isCollapsed === true) {
            return;
        }

        sidebar.style.width = currentWidth.toString() + "px";
    };

    const showBackdrop = () => {
        if (backdrop === null) {
            return;
        }

        backdrop.classList.remove("pointer-events-none");
        backdrop.classList.remove("opacity-0");
        backdrop.classList.add("opacity-100");
    };

    const hideBackdrop = () => {
        if (backdrop === null) {
            return;
        }

        backdrop.classList.add("pointer-events-none");
        backdrop.classList.remove("opacity-100");
        backdrop.classList.add("opacity-0");
    };

    const updateFloatingTrigger = () => {
        if (floatingTrigger === null) {
            return;
        }

        if (mediaQuery.matches === false) {
            floatingTrigger.classList.add("hidden");
            return;
        }

        if (isCollapsed === true) {
            floatingTrigger.classList.remove("hidden");
        }
        else {
            floatingTrigger.classList.add("hidden");
        }
    };

    const setCollapsed = (collapsed) => {
        isCollapsed = collapsed;

        if (collapsed === true) {
            sidebar.classList.add("is-collapsed");
            handle.classList.add("is-collapsed");
        }
        else {
            sidebar.classList.remove("is-collapsed");
            handle.classList.remove("is-collapsed");
            applyWidth();
        }

        updateFloatingTrigger();
    };

    const isMobileOpen = () => mediaQuery.matches === false && sidebar.classList.contains("translate-x-0");

    const openMobileSidebar = () => {
        if (mediaQuery.matches === true) {
            setCollapsed(false);
            return;
        }

        sidebar.classList.remove("-translate-x-full");
        sidebar.classList.add("translate-x-0");
        showBackdrop();
    };

    const closeMobileSidebar = () => {
        if (mediaQuery.matches === true) {
            setCollapsed(true);
            return;
        }

        sidebar.classList.remove("translate-x-0");
        sidebar.classList.add("-translate-x-full");
        hideBackdrop();
    };

    const toggleSidebar = () => {
        if (mediaQuery.matches === true) {
            setCollapsed(isCollapsed === false);
        }
        else if (isMobileOpen() === true) {
            closeMobileSidebar();
        }
        else {
            openMobileSidebar();
        }
    };

    const startDragging = (event) => {
        if (mediaQuery.matches === false || isCollapsed === true) {
            return;
        }

        isDragging = true;
        dragPointerId = event.pointerId;
        handle.setPointerCapture(event.pointerId);
        body.classList.add("select-none");
        event.preventDefault();
    };

    const updateDrag = (event) => {
        if (isDragging === false) {
            return;
        }

        const rootRect = layoutRoot.getBoundingClientRect();
        const desiredWidth = event.clientX - rootRect.left;
        const clampedWidth = clampWidth(desiredWidth);

        currentWidth = clampedWidth;
        sidebar.style.width = clampedWidth.toString() + "px";
    };

    const stopDragging = () => {
        if (isDragging === false) {
            return;
        }

        isDragging = false;

        if (dragPointerId !== null) {
            try {
                handle.releasePointerCapture(dragPointerId);
            }
            catch (error) {
                // Ignore failures to release pointer capture.
            }
        }

        dragPointerId = null;
        body.classList.remove("select-none");

        try {
            window.localStorage.setItem(storageKey, Math.round(currentWidth).toString());
        }
        catch (error) {
            // Ignored on purpose when localStorage is unavailable.
        }
    };

    const beginSwipe = (event) => {
        if (mediaQuery.matches === true) {
            return;
        }

        if (event.pointerType !== "touch" && event.pointerType !== "pen") {
            return;
        }

        swipeStartX = event.clientX;
    };

    const trackSwipe = (event) => {
        if (mediaQuery.matches === true || swipeStartX === null) {
            return;
        }

        const deltaX = event.clientX - swipeStartX;

        if (deltaX < -60) {
            swipeStartX = null;
            closeMobileSidebar();
        }
    };

    const endSwipe = () => {
        swipeStartX = null;
    };

    const handleBreakpointChange = (query) => {
        const matches = query.matches === true;

        if (matches === true) {
            hideBackdrop();
            sidebar.classList.remove("translate-x-0");
            setCollapsed(isCollapsed);
        }
        else {
            if (dragPointerId !== null) {
                try {
                    handle.releasePointerCapture(dragPointerId);
                }
                catch (error) {
                    // Ignore failures to release pointer capture.
                }
            }

            dragPointerId = null;
            isDragging = false;
            body.classList.remove("select-none");
            sidebar.style.removeProperty("width");
            sidebar.classList.remove("is-collapsed");
            handle.classList.remove("is-collapsed");
            hideBackdrop();
            sidebar.classList.remove("translate-x-0");
        }

        updateFloatingTrigger();
    };

    handle.addEventListener("pointerdown", startDragging);
    handle.addEventListener("pointermove", updateDrag);
    handle.addEventListener("pointerup", stopDragging);
    handle.addEventListener("pointercancel", stopDragging);

    sidebar.addEventListener("pointerdown", beginSwipe);
    sidebar.addEventListener("pointermove", trackSwipe);
    sidebar.addEventListener("pointerup", endSwipe);
    sidebar.addEventListener("pointercancel", endSwipe);

    window.addEventListener("resize", () => {
        currentWidth = clampWidth(currentWidth);

        if (mediaQuery.matches === true && isCollapsed === false) {
            applyWidth();
        }
    });

    if (typeof mediaQuery.addEventListener === "function") {
        mediaQuery.addEventListener("change", handleBreakpointChange);
    }
    else if (typeof mediaQuery.addListener === "function") {
        mediaQuery.addListener(handleBreakpointChange);
    }

    document.addEventListener("keydown", (event) => {
        if (event.altKey === true && event.key.toLowerCase() === "b") {
            event.preventDefault();
            toggleSidebar();
        }
        else if (event.key === "Escape") {
            if (mediaQuery.matches === true) {
                if (isCollapsed === false) {
                    setCollapsed(true);
                }
            }
            else if (isMobileOpen() === true) {
                closeMobileSidebar();
            }
        }
    });

    if (openButton !== null) {
        openButton.addEventListener("click", () => {
            if (mediaQuery.matches === true) {
                setCollapsed(false);
            }
            else {
                openMobileSidebar();
            }
        });
    }

    closeButtons.forEach((button) => {
        button.addEventListener("click", () => {
            if (mediaQuery.matches === true) {
                setCollapsed(true);
            }
            else {
                closeMobileSidebar();
            }
        });
    });

    if (backdrop !== null) {
        backdrop.addEventListener("click", () => {
            if (mediaQuery.matches === false) {
                closeMobileSidebar();
            }
        });
    }

    handleBreakpointChange(mediaQuery);
    applyWidth();

    if (floatingTrigger !== null) {
        floatingTrigger.addEventListener("click", () => {
            setCollapsed(false);
        });
    }
};
