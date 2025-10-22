// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", () => {
    initializeProfileMenu();
    initializeResizableSidebar();
    initializeCodexDrawer();
    initializeLevelSwitcher();
    initializeFlowchartTabs();
    initializeGitHubLinking();
    initializeGitHubPersonalAccessTokenForm();
});

let activeFlowchartTabManager = null;
let flowchartTabHotkeysBound = false;
let flowchartLevelChangeBound = false;

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

const initializeGitHubLinking = () => {
    const container = document.querySelector("[data-github-link]");
    if (container === null) {
        return;
    }

    const button = container.querySelector("[data-github-link-button]");
    const status = container.querySelector("[data-github-link-status]");
    if (button === null || status === null) {
        return;
    }

    const userId = container.getAttribute("data-user-id") ?? "";
    const redirectUri = container.getAttribute("data-redirect-uri") ?? "";
    const configuredAttribute = container.getAttribute("data-github-configured") ?? "false";
    const isConfigured = configuredAttribute.toLowerCase() === "true";

    if (isConfigured === false) {
        status.textContent = "Primero configura GitHub OAuth.";
        return;
    }

    let isProcessing = false;

    button.addEventListener("click", (event) => {
        event.preventDefault();

        if (isProcessing === true) {
            return;
        }

        if (userId.length === 0 || redirectUri.length === 0) {
            status.textContent = "No se pudo resolver el usuario ni el redirect.";
            return;
        }

        isProcessing = true;
        button.setAttribute("disabled", "disabled");
        status.textContent = "Redirigiendo a GitHub...";

        const payload = {
            userId: userId,
            redirectUri: redirectUri
        };

        fetch("/api/auth/github/start", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        })
            .then(async (response) => {
                if (!response.ok) {
                    if (response.status === 503) {
                        status.textContent = "GitHub OAuth aún no está configurado.";
                    }
                    else if (response.status === 400) {
                        status.textContent = "La petición es inválida.";
                    }
                    else {
                        status.textContent = "Hubo un error al iniciar la vinculación.";
                    }
                    throw new Error("GitHub OAuth start failed");
                }

                return await response.json();
            })
            .then((data) => {
                if (data === null || typeof data.authorizationUrl !== "string") {
                    status.textContent = "No se recibió la URL de autorización.";
                    return;
                }

                window.location.href = data.authorizationUrl;
            })
            .catch(() => {
                isProcessing = false;
                button.removeAttribute("disabled");
            });
    });
};

const initializeGitHubPersonalAccessTokenForm = () => {
    const container = document.querySelector("[data-github-pat]");
    if (container === null) {
        return;
    }

    const form = container.querySelector("[data-github-pat-form]");
    const input = container.querySelector("[data-github-pat-input]");
    const status = container.querySelector("[data-github-pat-status]");
    const submit = container.querySelector("[data-github-pat-submit]");
    const badge = container.querySelector("[data-github-pat-state]");
    const indicator = container.querySelector("[data-github-pat-indicator]");
    const label = container.querySelector("[data-github-pat-label]");

    if (form === null || input === null || status === null || submit === null) {
        return;
    }

    const endpoint = container.getAttribute("data-github-pat-endpoint") ?? "/api/github/personal-access-token";
    const statusEndpoint = container.getAttribute("data-github-pat-status-endpoint") ?? "";

    const statusStyles = ["text-gray-400", "text-emerald-300", "text-rose-300", "text-indigo-200"];

    const setStatus = (message, style) => {
        status.textContent = message;
        statusStyles.forEach((className) => status.classList.remove(className));
        status.classList.add(style);
    };

    const setSubmittingState = (isSubmitting) => {
        if (isSubmitting === true) {
            submit.setAttribute("disabled", "disabled");
            submit.classList.add("opacity-70", "cursor-not-allowed");
            input.setAttribute("disabled", "disabled");
        }
        else {
            submit.removeAttribute("disabled");
            submit.classList.remove("opacity-70", "cursor-not-allowed");
            input.removeAttribute("disabled");
        }
    };

    const updateBadge = (isConfigured) => {
        if (badge === null || indicator === null) {
            return;
        }

        badge.classList.remove("bg-emerald-500/10", "text-emerald-300", "bg-amber-500/10", "text-amber-200");
        indicator.classList.remove("bg-emerald-400", "bg-amber-400");

        if (label !== null) {
            label.textContent = isConfigured === true ? "Configurado" : "Pendiente";
        }

        if (isConfigured === true) {
            badge.classList.add("bg-emerald-500/10", "text-emerald-300");
            indicator.classList.add("bg-emerald-400");
        }
        else {
            badge.classList.add("bg-amber-500/10", "text-amber-200");
            indicator.classList.add("bg-amber-400");
        }
    };

    form.addEventListener("submit", (event) => {
        event.preventDefault();

        const token = input.value.trim();

        if (token.length === 0) {
            setStatus("Ingresa un token válido antes de guardarlo.", "text-rose-300");
            return;
        }

        setSubmittingState(true);
        setStatus("Guardando token...", "text-indigo-200");

        fetch(endpoint, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ token: token })
        })
            .then(async (response) => {
                if (response.ok) {
                    return;
                }

                if (response.status === 400) {
                    let detailMessage = "No se pudo validar el token enviado.";

                    try {
                        const problem = await response.json();
                        if (problem !== null && typeof problem === "object") {
                            if (typeof problem.detail === "string" && problem.detail.length > 0) {
                                detailMessage = problem.detail;
                            }
                            else if (problem.errors !== undefined && problem.errors !== null) {
                                const errorMessages = [];
                                Object.values(problem.errors).forEach((value) => {
                                    if (Array.isArray(value)) {
                                        value.forEach((item) => {
                                            if (typeof item === "string") {
                                                errorMessages.push(item);
                                            }
                                        });
                                    }
                                });

                                if (errorMessages.length > 0) {
                                    detailMessage = errorMessages.join(" ");
                                }
                            }
                        }
                    }
                    catch (error) {
                        // Ignore JSON parsing errors
                    }

                    setStatus(detailMessage, "text-rose-300");
                    throw new Error("validation");
                }

                setStatus("Ocurrió un error al guardar el token personal.", "text-rose-300");
                throw new Error("server");
            })
            .then(async () => {
                input.value = "";
                setStatus("Token guardado correctamente.", "text-emerald-300");
                updateBadge(true);

                if (statusEndpoint.length === 0) {
                    return;
                }

                try {
                    const statusResponse = await fetch(statusEndpoint, { method: "GET" });
                    if (statusResponse.ok) {
                        const payload = await statusResponse.json();
                        if (payload !== null && typeof payload === "object" && typeof payload.isConfigured === "boolean") {
                            updateBadge(payload.isConfigured === true);
                        }
                    }
                }
                catch (error) {
                    // Ignore refresh errors
                }
            })
            .catch((error) => {
                if (error instanceof Error) {
                    if (error.message === "validation" || error.message === "server") {
                        return;
                    }
                }

                setStatus("Ocurrió un error inesperado al guardar el token.", "text-rose-300");
            })
            .finally(() => {
                setSubmittingState(false);
            });
    });
};

const initializeCodexDrawer = () => {
    const drawer = document.querySelector("[data-codex-drawer]");
    const backdrop = document.querySelector("[data-codex-backdrop]");
    const floatingTrigger = document.querySelector("[data-codex-floating-trigger]");

    if (drawer === null || backdrop === null) {
        return;
    }

    const toggleButtons = Array.from(document.querySelectorAll("[data-codex-toggle]"));
    const closeButtons = Array.from(document.querySelectorAll("[data-codex-close]"));
    const input = drawer.querySelector("[data-codex-input]");
    let isOpen = false;
    let activeTrigger = null;

    const updateFloatingTriggerVisibility = () => {
        if (floatingTrigger === null) {
            return;
        }

        if (isOpen === true) {
            floatingTrigger.classList.add("hidden");
        }
        else {
            floatingTrigger.classList.remove("hidden");
        }
    };

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
        updateFloatingTriggerVisibility();
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

        updateFloatingTriggerVisibility();

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

    updateFloatingTriggerVisibility();
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

        const notifyLevelChange = () => {
            const eventDetail = { level: currentLevel };
            const levelChangeEvent = new CustomEvent("levelchange", { detail: eventDetail });

            switcher.dispatchEvent(levelChangeEvent);
        };

        const setCurrentLevel = (level) => {
            if (levels.includes(level) === false || level === currentLevel) {
                return;
            }

            currentLevel = level;
            currentIndex = levels.indexOf(currentLevel);
            switcher.setAttribute("data-current-level", currentLevel);
            render();
            notifyLevelChange();
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

        switcher.levelSwitcherController = {
            getCurrentLevel: () => currentLevel,
            getLevels: () => levels.slice(),
            setLevel: (level) => {
                setCurrentLevel(level);
            },
            goToPrevious: () => {
                goToPrevious();
            },
            goToNext: () => {
                goToNext();
            }
        };

        render();
        notifyLevelChange();
    });
};

const setActiveFlowchartTabManager = (manager) => {
    if (activeFlowchartTabManager === manager) {
        return;
    }

    activeFlowchartTabManager = manager;
};

const bindFlowchartTabHotkeys = () => {
    if (flowchartTabHotkeysBound === true) {
        return;
    }

    flowchartTabHotkeysBound = true;

    document.addEventListener("keydown", (event) => {
        const key = typeof event.key === "string" ? event.key.toLowerCase() : "";
        const usesAltModifier = event.altKey === true;
        const usesOtherModifiers = event.ctrlKey === true || event.metaKey === true;

        if (key !== "t" || usesAltModifier === false || usesOtherModifiers === true) {
            return;
        }

        if (activeFlowchartTabManager === null) {
            return;
        }

        event.preventDefault();

        if (event.shiftKey === true) {
            activeFlowchartTabManager.activatePrevious();
        }
        else {
            activeFlowchartTabManager.activateNext();
        }
    });
};

const bindFlowchartLevelChange = (switcherElement) => {
    if (switcherElement === null || flowchartLevelChangeBound === true) {
        return;
    }

    flowchartLevelChangeBound = true;

    switcherElement.addEventListener("levelchange", (event) => {
        if (activeFlowchartTabManager === null) {
            return;
        }

        const detail = event.detail;

        if (detail === null || typeof detail.level !== "string") {
            return;
        }

        activeFlowchartTabManager.updateActiveLevel(detail.level);
    });
};

const initializeFlowchartTabs = () => {
    const workspaceElements = Array.from(document.querySelectorAll("[data-flowchart-tabs-root]"));

    if (workspaceElements.length === 0) {
        return;
    }

    const levelSwitcherElement = document.querySelector("[data-level-switcher]");

    bindFlowchartTabHotkeys();
    bindFlowchartLevelChange(levelSwitcherElement);

    workspaceElements.forEach((workspace, workspaceIndex) => {
        const tablist = workspace.querySelector("[data-flowchart-tablist]");
        const mainSection = workspace.querySelector("[data-flowchart-main]");
        const emptyState = workspace.querySelector("[data-flowchart-empty-state]");
        const statusContainer = workspace.querySelector("[data-flowchart-status]");
        const statusLabel = statusContainer !== null ? statusContainer.querySelector("[data-flowchart-status-label]") : null;
        const levelBadge = workspace.querySelector("[data-flowchart-level-badge]");
        const titleElement = workspace.querySelector("[data-flowchart-title]");
        const summaryElement = workspace.querySelector("[data-flowchart-summary]");
        const tagsContainer = workspace.querySelector("[data-flowchart-tags]");
        const ownerElement = workspace.querySelector("[data-flowchart-owner]");
        const roleElement = workspace.querySelector("[data-flowchart-role]");
        const updatedElement = workspace.querySelector("[data-flowchart-updated]");
        const canvasLevelElement = workspace.querySelector("[data-flowchart-canvas-level]");
        const canvasDescriptionElement = workspace.querySelector("[data-flowchart-canvas-description]");
        const metricsContainer = workspace.querySelector("[data-flowchart-metrics]");
        const highlightsContainer = workspace.querySelector("[data-flowchart-highlights]");

        if (tablist === null) {
            return;
        }

        const rawFlowcharts = workspace.getAttribute("data-flowcharts");
        let parsedFlowcharts = [];

        if (typeof rawFlowcharts === "string" && rawFlowcharts.trim().length > 0) {
            try {
                const parsedJson = JSON.parse(rawFlowcharts);

                if (Array.isArray(parsedJson) === true) {
                    parsedFlowcharts = parsedJson;
                }
            }
            catch (error) {
                // Ignored intentionally when the JSON cannot be parsed.
            }
        }

        const toNonEmptyString = (value) => {
            if (typeof value === "string") {
                const trimmedValue = value.trim();

                if (trimmedValue.length > 0) {
                    return trimmedValue;
                }

                return null;
            }

            if (typeof value === "number" && Number.isFinite(value) === true) {
                return value.toString();
            }

            return null;
        };

        const sanitizeFlowcharts = () => {
            const sanitized = [];

            parsedFlowcharts.forEach((item, index) => {
                if (item === null || typeof item !== "object") {
                    return;
                }

                let identifier = toNonEmptyString(item.id);

                if (identifier === null) {
                    identifier = "flowchart-" + (index + 1).toString();
                }

                let titleValue = toNonEmptyString(item.title);

                if (titleValue === null) {
                    titleValue = "Flowchart " + (index + 1).toString();
                }

                let summaryValue = toNonEmptyString(item.summary);

                if (summaryValue === null) {
                    summaryValue = "";
                }

                let ownerValue = toNonEmptyString(item.owner);

                if (ownerValue === null) {
                    ownerValue = "";
                }

                let ownerRoleValue = toNonEmptyString(item.ownerRole);

                if (ownerRoleValue === null) {
                    ownerRoleValue = "";
                }

                let updatedValue = toNonEmptyString(item.updated);

                if (updatedValue === null) {
                    updatedValue = "";
                }

                let statusValue = toNonEmptyString(item.status);

                if (statusValue === null) {
                    statusValue = "Sin estado";
                }

                let canvasDescriptionValue = toNonEmptyString(item.canvasDescription);

                if (canvasDescriptionValue === null) {
                    canvasDescriptionValue = "";
                }

                let levels = Array.isArray(item.levels) === true ? item.levels : [];

                levels = levels
                    .map((level) => toNonEmptyString(level))
                    .filter((level) => level !== null);

                if (levels.length === 0) {
                    levels = ["L1", "L2", "L3"];
                }

                const uniqueLevels = Array.from(new Set(levels));
                let currentLevelValue = toNonEmptyString(item.currentLevel);

                if (currentLevelValue === null || uniqueLevels.includes(currentLevelValue) === false) {
                    currentLevelValue = uniqueLevels[0];
                }

                const metricsSource = Array.isArray(item.metrics) === true ? item.metrics : [];
                const metrics = metricsSource
                    .map((metric) => {
                        if (metric === null || typeof metric !== "object") {
                            return null;
                        }

                        const labelValue = toNonEmptyString(metric.label);
                        const metricValue = toNonEmptyString(metric.value);

                        if (labelValue === null || metricValue === null) {
                            return null;
                        }

                        return { label: labelValue, value: metricValue };
                    })
                    .filter((metric) => metric !== null);

                const highlightsSource = Array.isArray(item.highlights) === true ? item.highlights : [];
                const highlights = highlightsSource
                    .map((highlight) => {
                        if (highlight === null || typeof highlight !== "object") {
                            return null;
                        }

                        let highlightTitle = toNonEmptyString(highlight.title);

                        if (highlightTitle === null) {
                            highlightTitle = "Detalle clave";
                        }

                        let highlightDescription = toNonEmptyString(highlight.description);

                        if (highlightDescription === null) {
                            highlightDescription = "";
                        }

                        return { title: highlightTitle, description: highlightDescription };
                    })
                    .filter((highlight) => highlight !== null);

                const tagsSource = Array.isArray(item.tags) === true ? item.tags : [];
                const tags = tagsSource
                    .map((tag) => toNonEmptyString(tag))
                    .filter((tag) => tag !== null);

                sanitized.push({
                    id: identifier,
                    title: titleValue,
                    summary: summaryValue,
                    owner: ownerValue,
                    ownerRole: ownerRoleValue,
                    updated: updatedValue,
                    status: statusValue,
                    currentLevel: currentLevelValue,
                    levels: uniqueLevels,
                    canvasDescription: canvasDescriptionValue,
                    metrics,
                    highlights,
                    tags
                });
            });

            return sanitized;
        };

        const flowcharts = sanitizeFlowcharts();

        const state = {
            flowcharts,
            activeId: flowcharts.length > 0 ? flowcharts[0].id : null
        };

        const levelController = levelSwitcherElement !== null ? levelSwitcherElement.levelSwitcherController : null;
        let manager = null;

        const setCanvasVisibility = (isEmpty) => {
            if (mainSection !== null) {
                if (isEmpty === true) {
                    mainSection.classList.add("hidden");
                }
                else {
                    mainSection.classList.remove("hidden");
                }
            }

            if (emptyState !== null) {
                if (isEmpty === true) {
                    emptyState.classList.remove("hidden");
                }
                else {
                    emptyState.classList.add("hidden");
                }
            }
        };

        const renderTags = (tags) => {
            if (tagsContainer === null) {
                return;
            }

            tagsContainer.innerHTML = "";

            if (tags.length === 0) {
                const placeholder = document.createElement("span");
                placeholder.classList.add("inline-flex", "items-center", "rounded-full", "border", "border-dashed", "border-white/20", "px-3", "py-1", "text-xs", "text-gray-500");
                placeholder.textContent = "Sin etiquetas asignadas";

                tagsContainer.appendChild(placeholder);
                return;
            }

            tags.forEach((tag) => {
                if (tag === null) {
                    return;
                }

                const badge = document.createElement("span");
                badge.classList.add("inline-flex", "items-center", "rounded-full", "bg-indigo-500/10", "px-3", "py-1", "text-xs", "font-medium", "text-indigo-200");
                badge.textContent = tag;

                tagsContainer.appendChild(badge);
            });
        };

        const renderMetrics = (metrics) => {
            if (metricsContainer === null) {
                return;
            }

            metricsContainer.innerHTML = "";

            if (metrics.length === 0) {
                const emptyMetric = document.createElement("div");
                emptyMetric.classList.add("sm:col-span-3", "rounded-xl", "border", "border-dashed", "border-white/10", "bg-transparent", "p-4", "text-sm", "text-gray-400");
                emptyMetric.textContent = "Sin métricas registradas para este nivel.";

                metricsContainer.appendChild(emptyMetric);
                return;
            }

            metrics.forEach((metric) => {
                if (metric === null) {
                    return;
                }

                const card = document.createElement("div");
                card.classList.add("rounded-xl", "border", "border-white/10", "bg-white/5", "px-4", "py-4", "shadow-inner", "shadow-black/20");

                const term = document.createElement("dt");
                term.classList.add("text-xs", "font-semibold", "uppercase", "tracking-wide", "text-indigo-300");
                term.textContent = metric.label;

                const value = document.createElement("dd");
                value.classList.add("mt-2", "text-2xl", "font-semibold", "text-white");
                value.textContent = metric.value;

                card.appendChild(term);
                card.appendChild(value);
                metricsContainer.appendChild(card);
            });
        };

        const renderHighlights = (highlights) => {
            if (highlightsContainer === null) {
                return;
            }

            highlightsContainer.innerHTML = "";

            if (highlights.length === 0) {
                const placeholderItem = document.createElement("li");
                placeholderItem.classList.add("rounded-xl", "border", "border-dashed", "border-white/10", "bg-transparent", "p-4", "text-sm", "text-gray-400");
                placeholderItem.textContent = "Sin notas registradas para este flowchart.";

                highlightsContainer.appendChild(placeholderItem);
                return;
            }

            highlights.forEach((highlight) => {
                if (highlight === null) {
                    return;
                }

                const item = document.createElement("li");
                item.classList.add("rounded-xl", "border", "border-white/10", "bg-white/5", "p-4", "shadow-inner", "shadow-black/20");

                const highlightTitleElement = document.createElement("p");
                highlightTitleElement.classList.add("text-sm", "font-semibold", "text-white");
                highlightTitleElement.textContent = highlight.title;

                const highlightDescriptionElement = document.createElement("p");
                highlightDescriptionElement.classList.add("mt-1", "text-sm", "text-gray-300");
                highlightDescriptionElement.textContent = highlight.description;

                item.appendChild(highlightTitleElement);
                item.appendChild(highlightDescriptionElement);
                highlightsContainer.appendChild(item);
            });
        };

        const updateLevelIndicators = (flowchart) => {
            if (levelBadge !== null) {
                levelBadge.textContent = "Nivel actual: " + flowchart.currentLevel;
            }

            if (canvasLevelElement !== null) {
                canvasLevelElement.textContent = "Nivel " + flowchart.currentLevel;
            }
        };

        const updateCanvas = (flowchart) => {
            if (flowchart === null) {
                setCanvasVisibility(true);

                if (workspace.hasAttribute("data-active-flowchart") === true) {
                    workspace.removeAttribute("data-active-flowchart");
                }

                return;
            }

            setCanvasVisibility(false);
            workspace.setAttribute("data-active-flowchart", flowchart.id);

            if (statusLabel !== null) {
                statusLabel.textContent = flowchart.status;
            }

            if (titleElement !== null) {
                titleElement.textContent = flowchart.title;
            }

            if (summaryElement !== null) {
                summaryElement.textContent = flowchart.summary;
            }

            renderTags(flowchart.tags);

            if (ownerElement !== null) {
                ownerElement.textContent = flowchart.owner;
            }

            if (roleElement !== null) {
                roleElement.textContent = flowchart.ownerRole;
            }

            if (updatedElement !== null) {
                updatedElement.textContent = flowchart.updated;
            }

            if (canvasDescriptionElement !== null) {
                canvasDescriptionElement.textContent = flowchart.canvasDescription;
            }

            updateLevelIndicators(flowchart);
            renderMetrics(flowchart.metrics);
            renderHighlights(flowchart.highlights);
        };

        const focusActiveTab = () => {
            if (tablist === null) {
                return;
            }

            const activeButton = tablist.querySelector("[data-flowchart-tab][aria-selected='true']");

            if (activeButton instanceof HTMLElement) {
                activeButton.focus();

                if (typeof activeButton.scrollIntoView === "function") {
                    activeButton.scrollIntoView({ block: "nearest", inline: "nearest" });
                }
            }
        };

        const renderTabs = () => {
            if (tablist === null) {
                return;
            }

            tablist.innerHTML = "";

            state.flowcharts.forEach((flowchart) => {
                const listItem = document.createElement("li");
                listItem.classList.add("flex", "items-center");

                const button = document.createElement("button");
                button.type = "button";
                button.dataset.flowchartTab = flowchart.id;
                button.setAttribute("role", "tab");

                const isActive = flowchart.id === state.activeId;

                button.setAttribute("aria-selected", isActive === true ? "true" : "false");
                button.setAttribute("tabindex", isActive === true ? "0" : "-1");

                const baseClasses = [
                    "group",
                    "inline-flex",
                    "items-center",
                    "gap-2",
                    "rounded-xl",
                    "px-3",
                    "py-2",
                    "text-sm",
                    "font-medium",
                    "transition",
                    "focus-visible:outline",
                    "focus-visible:outline-2",
                    "focus-visible:outline-offset-2",
                    "focus-visible:outline-indigo-500"
                ];

                const activeClasses = [
                    "bg-indigo-500/20",
                    "text-indigo-100",
                    "shadow-inner",
                    "shadow-indigo-950/40",
                    "ring-1",
                    "ring-inset",
                    "ring-indigo-500/40"
                ];

                const inactiveClasses = [
                    "text-gray-300",
                    "hover:bg-white/5",
                    "hover:text-white"
                ];

                baseClasses.forEach((className) => button.classList.add(className));

                if (isActive === true) {
                    button.dataset.flowchartTabActive = "true";
                    activeClasses.forEach((className) => button.classList.add(className));
                }
                else {
                    button.dataset.flowchartTabActive = "false";
                    inactiveClasses.forEach((className) => button.classList.add(className));
                }

                const labelElement = document.createElement("span");
                labelElement.classList.add("truncate", "max-w-[10rem]");
                labelElement.textContent = flowchart.title;

                const closeButton = document.createElement("button");
                closeButton.type = "button";
                closeButton.classList.add(
                    "inline-flex",
                    "size-6",
                    "items-center",
                    "justify-center",
                    "rounded-md",
                    "text-gray-400",
                    "transition",
                    "hover:bg-white/10",
                    "hover:text-white",
                    "focus-visible:outline",
                    "focus-visible:outline-2",
                    "focus-visible:outline-offset-2",
                    "focus-visible:outline-indigo-500"
                );
                closeButton.setAttribute("aria-label", "Cerrar " + flowchart.title);
                closeButton.innerHTML = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" aria-hidden=\"true\" class=\"size-4\"><path d=\"M6 18 18 6M6 6l12 12\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path></svg>";

                closeButton.addEventListener("click", (event) => {
                    event.preventDefault();
                    event.stopPropagation();

                    if (manager !== null) {
                        manager.removeFlowchart(flowchart.id);
                    }
                });

                button.addEventListener("click", (event) => {
                    event.preventDefault();
                    setActiveFlowchartTabManager(manager);
                    selectFlowchart(flowchart.id, false);
                });

                button.appendChild(labelElement);
                button.appendChild(closeButton);

                listItem.appendChild(button);
                tablist.appendChild(listItem);
            });
        };

        const syncLevelSwitcher = (flowchart) => {
            if (flowchart === null || levelController === null || typeof levelController.setLevel !== "function") {
                return;
            }

            levelController.setLevel(flowchart.currentLevel);
        };

        const getActiveFlowchart = () => {
            if (state.activeId === null) {
                return null;
            }

            const match = state.flowcharts.find((item) => item.id === state.activeId);

            if (typeof match === "undefined") {
                return null;
            }

            return match;
        };

        const selectFlowchart = (id, shouldFocus) => {
            const targetFlowchart = state.flowcharts.find((item) => item.id === id);

            if (typeof targetFlowchart === "undefined") {
                return;
            }

            state.activeId = targetFlowchart.id;
            renderTabs();
            updateCanvas(targetFlowchart);
            syncLevelSwitcher(targetFlowchart);

            if (shouldFocus === true) {
                focusActiveTab();
            }
        };

        const removeFlowchart = (id) => {
            const index = state.flowcharts.findIndex((item) => item.id === id);

            if (index === -1) {
                return;
            }

            state.flowcharts.splice(index, 1);

            if (state.flowcharts.length === 0) {
                state.activeId = null;
                renderTabs();
                updateCanvas(null);
                return;
            }

            if (state.activeId === id) {
                const nextIndex = index >= state.flowcharts.length ? state.flowcharts.length - 1 : index;
                state.activeId = state.flowcharts[nextIndex].id;
            }

            renderTabs();
            const activeFlowchart = getActiveFlowchart();
            updateCanvas(activeFlowchart);
            syncLevelSwitcher(activeFlowchart);
        };

        const activateNext = () => {
            if (state.flowcharts.length === 0) {
                return;
            }

            if (state.activeId === null) {
                const firstFlowchart = state.flowcharts[0];
                selectFlowchart(firstFlowchart.id, true);
                return;
            }

            const currentIndex = state.flowcharts.findIndex((item) => item.id === state.activeId);
            const nextIndex = currentIndex === -1 ? 0 : (currentIndex + 1) % state.flowcharts.length;

            selectFlowchart(state.flowcharts[nextIndex].id, true);
        };

        const activatePrevious = () => {
            if (state.flowcharts.length === 0) {
                return;
            }

            if (state.activeId === null) {
                const lastFlowchart = state.flowcharts[state.flowcharts.length - 1];
                selectFlowchart(lastFlowchart.id, true);
                return;
            }

            const currentIndex = state.flowcharts.findIndex((item) => item.id === state.activeId);
            const previousIndex = currentIndex === -1 ? state.flowcharts.length - 1 : (currentIndex - 1 + state.flowcharts.length) % state.flowcharts.length;

            selectFlowchart(state.flowcharts[previousIndex].id, true);
        };

        const updateActiveLevel = (level) => {
            const activeFlowchart = getActiveFlowchart();

            if (activeFlowchart === null) {
                return;
            }

            if (activeFlowchart.levels.includes(level) === false || activeFlowchart.currentLevel === level) {
                return;
            }

            activeFlowchart.currentLevel = level;
            updateLevelIndicators(activeFlowchart);
        };

        manager = {
            removeFlowchart,
            selectFlowchart,
            activateNext,
            activatePrevious,
            updateActiveLevel
        };

        renderTabs();

        const initialFlowchart = getActiveFlowchart();

        if (initialFlowchart === null) {
            setCanvasVisibility(true);
        }
        else {
            updateCanvas(initialFlowchart);
            syncLevelSwitcher(initialFlowchart);
        }

        if (workspaceIndex === 0) {
            setActiveFlowchartTabManager(manager);
        }

        workspace.addEventListener("pointerdown", () => {
            setActiveFlowchartTabManager(manager);
        });

        workspace.addEventListener("focusin", () => {
            setActiveFlowchartTabManager(manager);
        });
    });
};
