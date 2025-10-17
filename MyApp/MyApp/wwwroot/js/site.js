// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", () => {
    initializeProfileMenu();
    initializeResizableSidebar();
    initializeCodexDrawer();
    initializeLevelSwitcher();
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

const initializeCodexDrawer = () => {
    const drawer = document.querySelector("[data-codex-drawer]");
    const backdrop = document.querySelector("[data-codex-backdrop]");

    if (drawer === null || backdrop === null) {
        return;
    }

    const toggleButtons = Array.from(document.querySelectorAll("[data-codex-toggle]"));
    const closeButtons = Array.from(document.querySelectorAll("[data-codex-close]"));
    const input = drawer.querySelector("[data-codex-input]");
    let isOpen = false;
    let activeTrigger = null;

    const focusInput = () => {
        if (input instanceof HTMLElement === false) {
            return;
        }

        window.requestAnimationFrame(() => {
            if (input instanceof HTMLElement) {
                input.focus();
            }
        });
    };

    const openDrawer = (trigger) => {
        if (isOpen === true) {
            return;
        }

        isOpen = true;
        activeTrigger = trigger instanceof HTMLElement ? trigger : null;
        drawer.classList.remove("translate-x-full");
        drawer.classList.add("translate-x-0");
        drawer.setAttribute("aria-hidden", "false");
        backdrop.classList.remove("pointer-events-none");
        backdrop.classList.remove("opacity-0");
        backdrop.classList.add("opacity-100");
        focusInput();
    };

    const closeDrawer = () => {
        if (isOpen === false) {
            return;
        }

        isOpen = false;
        drawer.classList.add("translate-x-full");
        drawer.classList.remove("translate-x-0");
        drawer.setAttribute("aria-hidden", "true");
        backdrop.classList.add("opacity-0");
        backdrop.classList.remove("opacity-100");
        backdrop.classList.add("pointer-events-none");

        if (activeTrigger instanceof HTMLElement) {
            activeTrigger.focus();
        }

        activeTrigger = null;
    };

    const toggleDrawer = (trigger) => {
        if (isOpen === true) {
            closeDrawer();
        }
        else {
            openDrawer(trigger);
        }
    };

    toggleButtons.forEach((button) => {
        button.addEventListener("click", (event) => {
            event.preventDefault();
            toggleDrawer(button);
        });
    });

    closeButtons.forEach((button) => {
        button.addEventListener("click", (event) => {
            event.preventDefault();
            closeDrawer();
        });
    });

    backdrop.addEventListener("click", () => {
        closeDrawer();
    });

    document.addEventListener("keydown", (event) => {
        if (event.altKey === true) {
            const key = typeof event.key === "string" ? event.key.toLowerCase() : "";

            if (key === "c") {
                event.preventDefault();
                toggleDrawer(null);
                return;
            }
        }

        if (event.key === "Escape" && isOpen === true) {
            closeDrawer();
        }
    });
};

const initializeLevelSwitcher = () => {
    const switchers = Array.from(document.querySelectorAll("[data-level-switcher]"));

    if (switchers.length === 0) {
        return;
    }

    switchers.forEach((switcher) => {
        const pagesContainer = switcher.querySelector("[data-level-pages]");
        const previousButton = switcher.querySelector("[data-level-previous]");
        const nextButton = switcher.querySelector("[data-level-next]");

        if (pagesContainer === null || previousButton === null || nextButton === null) {
            return;
        }

        const container = switcher.closest("[data-level-switcher-container]");
        const mobilePrevious = container !== null ? container.querySelector("[data-level-mobile-previous]") : null;
        const mobileNext = container !== null ? container.querySelector("[data-level-mobile-next]") : null;

        const rawLevels = switcher.getAttribute("data-levels");
        let levels = [];

        if (typeof rawLevels === "string" && rawLevels.trim().length > 0) {
            try {
                const parsedLevels = JSON.parse(rawLevels);

                if (Array.isArray(parsedLevels) === true) {
                    levels = parsedLevels;
                }
            }
            catch (error) {
                // Ignored when the level list cannot be parsed.
            }
        }

        if (levels.length === 0) {
            const fallbackButtons = Array.from(switcher.querySelectorAll("[data-level-button]"));

            if (fallbackButtons.length > 0) {
                levels = fallbackButtons
                    .map((button) => button.getAttribute("data-level-button"))
                    .filter((value) => typeof value === "string" && value.length > 0);
            }
        }

        levels = Array.from(new Set(levels.filter((value) => typeof value === "string" && value.length > 0)));

        if (levels.length === 0) {
            return;
        }

        switcher.setAttribute("data-levels", JSON.stringify(levels));

        let currentLevel = switcher.getAttribute("data-current-level");

        if (typeof currentLevel !== "string" || levels.includes(currentLevel) === false) {
            currentLevel = levels[0];
        }

        let currentIndex = levels.indexOf(currentLevel);
        const maxVisible = 7;

        const applyDisabledState = (button, disabled) => {
            if (button === null) {
                return;
            }

            if (disabled === true) {
                button.classList.add("cursor-not-allowed", "opacity-40");
                button.setAttribute("aria-disabled", "true");
                button.setAttribute("disabled", "");
            }
            else {
                button.classList.remove("cursor-not-allowed", "opacity-40");
                button.removeAttribute("aria-disabled");
                button.removeAttribute("disabled");
            }
        };

        const setCurrentLevel = (level) => {
            if (levels.includes(level) === false) {
                return;
            }

            currentLevel = level;
            currentIndex = levels.indexOf(currentLevel);
            switcher.setAttribute("data-current-level", currentLevel);
            render();
        };

        const goToPrevious = () => {
            if (currentIndex <= 0) {
                return;
            }

            setCurrentLevel(levels[currentIndex - 1]);
        };

        const goToNext = () => {
            if (currentIndex >= levels.length - 1) {
                return;
            }

            setCurrentLevel(levels[currentIndex + 1]);
        };

        const createLevelButton = (level, isActive) => {
            const button = document.createElement("button");

            button.type = "button";
            button.dataset.levelButton = level;
            button.setAttribute("aria-pressed", isActive === true ? "true" : "false");
            button.textContent = level;

            const baseButtonClasses = [
                "relative",
                "inline-flex",
                "items-center",
                "px-4",
                "py-2",
                "text-sm",
                "font-semibold",
                "focus:z-20"
            ];

            const activeButtonClasses = [
                "z-10",
                "bg-indigo-500",
                "text-white",
                "shadow-lg",
                "shadow-indigo-950/40",
                "focus-visible:outline-2",
                "focus-visible:outline-offset-2",
                "focus-visible:outline-indigo-500"
            ];

            const inactiveButtonClasses = [
                "text-gray-200",
                "inset-ring",
                "inset-ring-gray-700",
                "hover:bg-white/5",
                "focus:outline-offset-0"
            ];

            baseButtonClasses.forEach((className) => button.classList.add(className));

            if (isActive === true) {
                activeButtonClasses.forEach((className) => button.classList.add(className));
            }
            else {
                inactiveButtonClasses.forEach((className) => button.classList.add(className));
            }

            button.addEventListener("click", (event) => {
                event.preventDefault();
                setCurrentLevel(level);
            });

            return button;
        };

        const createEllipsis = () => {
            const ellipsis = document.createElement("span");

            ellipsis.classList.add(
                "relative",
                "inline-flex",
                "items-center",
                "px-4",
                "py-2",
                "text-sm",
                "font-semibold",
                "text-gray-400",
                "inset-ring",
                "inset-ring-gray-700",
                "focus:outline-offset-0"
            );

            ellipsis.setAttribute("aria-hidden", "true");
            ellipsis.textContent = "...";

            return ellipsis;
        };

        const render = () => {
            pagesContainer.innerHTML = "";

            if (levels.includes(currentLevel) === false) {
                currentLevel = levels[0];
                currentIndex = 0;
            }

            const targetSize = Math.min(maxVisible, levels.length);
            let visibleIndices;

            if (levels.length <= maxVisible) {
                visibleIndices = levels.map((_, index) => index);
            }
            else {
                const indexSet = new Set();

                indexSet.add(0);
                indexSet.add(levels.length - 1);
                indexSet.add(currentIndex);

                let leftIndex = currentIndex - 1;
                let rightIndex = currentIndex + 1;

                while (indexSet.size < targetSize && (leftIndex > 0 || rightIndex < levels.length - 1)) {
                    if (leftIndex > 0) {
                        indexSet.add(leftIndex);
                        leftIndex -= 1;
                    }

                    if (indexSet.size >= targetSize) {
                        break;
                    }

                    if (rightIndex < levels.length - 1) {
                        indexSet.add(rightIndex);
                        rightIndex += 1;
                    }
                }

                visibleIndices = Array.from(indexSet).sort((a, b) => a - b);

                if (visibleIndices.length < targetSize) {
                    for (let index = 1; index < levels.length - 1 && visibleIndices.length < targetSize; index++) {
                        if (visibleIndices.includes(index) === false) {
                            visibleIndices.push(index);
                        }
                    }

                    visibleIndices.sort((a, b) => a - b);
                }
            }

            let lastIndex = null;

            visibleIndices.forEach((index) => {
                if (lastIndex !== null && index - lastIndex > 1) {
                    pagesContainer.appendChild(createEllipsis());
                }

                const level = levels[index];
                const isActive = level === currentLevel;

                pagesContainer.appendChild(createLevelButton(level, isActive));

                lastIndex = index;
            });

            applyDisabledState(previousButton, currentIndex === 0);
            applyDisabledState(nextButton, currentIndex === levels.length - 1);
            applyDisabledState(mobilePrevious, currentIndex === 0);
            applyDisabledState(mobileNext, currentIndex === levels.length - 1);
        };

        previousButton.addEventListener("click", (event) => {
            event.preventDefault();
            goToPrevious();
        });

        nextButton.addEventListener("click", (event) => {
            event.preventDefault();
            goToNext();
        });

        if (mobilePrevious !== null) {
            mobilePrevious.addEventListener("click", (event) => {
                event.preventDefault();
                goToPrevious();
            });
        }

        if (mobileNext !== null) {
            mobileNext.addEventListener("click", (event) => {
                event.preventDefault();
                goToNext();
            });
        }

        render();
    });
};
