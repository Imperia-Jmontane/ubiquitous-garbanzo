const initializeCodeAnalysisPage = () => {
    const root = document.querySelector("[data-code-analysis-root]");

    if (root === null) {
        return;
    }

    let repositoryId = root.dataset.repositoryId || "";
    const repositorySelector = root.querySelector("[data-repository-selector]");
    const startIndexingButton = root.querySelector("[data-start-indexing]");
    const statusBadge = root.querySelector("[data-indexing-status]");
    const statusText = root.querySelector("[data-indexing-text]");
    const statusDot = root.querySelector("[data-indexing-dot]");
    const graphPlaceholder = root.querySelector("[data-graph-placeholder]");
    const layoutSelector = root.querySelector("[data-layout-selector]");
    const fitButton = root.querySelector("[data-fit-graph]");
    const exportButton = root.querySelector("[data-export-graph]");
    const searchInput = root.querySelector("[data-symbol-search]");
    const filterKinds = root.querySelectorAll("[data-filter-kind]");
    const namespaceFilter = root.querySelector("[data-namespace-filter]");
    const depthSlider = root.querySelector("[data-depth-slider]");
    const depthValue = root.querySelector("[data-depth-value]");
    const neighborToggle = root.querySelector("[data-neighbor-toggle]");
    const symbolName = root.querySelector("[data-symbol-name]");
    const symbolKind = root.querySelector("[data-symbol-kind]");
    const symbolFullName = root.querySelector("[data-symbol-full-name]");
    const symbolLocation = root.querySelector("[data-symbol-location]");
    const symbolModifiers = root.querySelector("[data-symbol-modifiers]");
    const viewInFileLink = root.querySelector("[data-view-in-file]");
    const sourceFile = root.querySelector("[data-source-file]");
    const sourceRange = root.querySelector("[data-source-range]");
    const sourceCode = root.querySelector("[data-source-code]");
    const referencesItems = root.querySelector("[data-references-items]");
    const symbolTree = root.querySelector("[data-symbol-tree]");

    const statusClasses = ["text-emerald-300", "text-amber-300", "text-sky-300", "text-rose-300", "text-gray-300"];
    const dotClasses = ["bg-emerald-400", "bg-amber-400", "bg-sky-400", "bg-rose-400", "bg-gray-400"];
    let activeRenderer = null;
    let statusInterval = null;
    let searchTimeout = null;
    let lastSelectedNodeId = null;

    const loadRepositories = async () => {
        if (repositorySelector === null) {
            return;
        }

        try {
            const response = await fetch("/api/code-analysis/repositories");
            if (!response.ok) {
                return;
            }

            const repositories = await response.json();
            repositorySelector.innerHTML = "<option value=\"\">Select a repository...</option>";

            if (!Array.isArray(repositories) || repositories.length === 0) {
                repositorySelector.innerHTML = "<option value=\"\">No repositories available</option>";
                return;
            }

            repositories.forEach((repository) => {
                const option = document.createElement("option");
                option.value = repository.id;
                option.textContent = repository.name;
                if (repository.id === repositoryId) {
                    option.selected = true;
                }
                repositorySelector.appendChild(option);
            });

            updateRepositorySelection();
        } catch (error) {
            console.warn("Failed to load repositories", error);
        }
    };

    const updateRepositorySelection = () => {
        const hasRepository = repositoryId.length > 0;

        if (startIndexingButton !== null) {
            if (hasRepository) {
                startIndexingButton.removeAttribute("disabled");
            } else {
                startIndexingButton.setAttribute("disabled", "disabled");
            }
        }

        if (symbolTree !== null) {
            if (hasRepository) {
                symbolTree.innerHTML = "<p class=\"text-xs uppercase tracking-wide text-gray-500\">Symbol tree</p><p class=\"mt-2\">Loading symbols...</p>";
            } else {
                symbolTree.innerHTML = "<p class=\"text-xs uppercase tracking-wide text-gray-500\">Symbol tree</p><p class=\"mt-2\">Select a repository to load symbols.</p>";
            }
        }

        if (hasRepository) {
            updateStatusBadge("Ready to index");
            setPlaceholderText("Start indexing to render the graph.");
        } else {
            updateStatusBadge("No repository");
            setPlaceholderText("Select a repository to begin.");
        }
    };

    const onRepositoryChange = () => {
        if (repositorySelector === null) {
            return;
        }

        repositoryId = repositorySelector.value;
        root.dataset.repositoryId = repositoryId;

        if (statusInterval !== null) {
            window.clearInterval(statusInterval);
            statusInterval = null;
        }

        if (activeRenderer !== null && typeof activeRenderer.clearNeighborFocus === "function") {
            activeRenderer.clearNeighborFocus();
        }

        updateRepositorySelection();

        if (repositoryId.length > 0) {
            updateStatusFromApi();
        }
    };

    const updateStatusBadge = (statusLabel) => {
        if (statusText !== null) {
            statusText.textContent = statusLabel;
        }

        if (statusBadge !== null) {
            statusBadge.classList.remove(...statusClasses);
        }

        if (statusDot !== null) {
            statusDot.classList.remove(...dotClasses);
        }

        const normalized = (statusLabel || "").toLowerCase();
        let badgeClass = "text-gray-300";
        let dotClass = "bg-gray-400";

        if (normalized.includes("queued")) {
            badgeClass = "text-amber-300";
            dotClass = "bg-amber-400";
        } else if (normalized.includes("running")) {
            badgeClass = "text-sky-300";
            dotClass = "bg-sky-400";
        } else if (normalized.includes("failed")) {
            badgeClass = "text-rose-300";
            dotClass = "bg-rose-400";
        } else if (normalized.includes("complete")) {
            badgeClass = "text-emerald-300";
            dotClass = "bg-emerald-400";
        }

        if (statusBadge !== null) {
            statusBadge.classList.add(badgeClass);
        }

        if (statusDot !== null) {
            statusDot.classList.add(dotClass);
        }
    };

    const setPlaceholderText = (message) => {
        if (graphPlaceholder === null) {
            return;
        }

        graphPlaceholder.textContent = message;
    };

    const getSelectedKinds = () => {
        const selectedKinds = [];

        filterKinds.forEach((checkbox) => {
            if (checkbox instanceof HTMLInputElement && checkbox.checked) {
                const kind = checkbox.dataset.filterKind;
                if (kind) {
                    selectedKinds.push(kind);
                }
            }
        });

        return selectedKinds;
    };

    const buildGraphQuery = () => {
        const params = new URLSearchParams();

        params.set("repositoryId", repositoryId);
        params.set("maxDepth", depthSlider instanceof HTMLInputElement ? depthSlider.value : "2");
        params.set("maxNodes", "250");
        params.set("maxEdges", "800");
        params.set("includeMembers", "true");

        if (namespaceFilter instanceof HTMLInputElement && namespaceFilter.value.trim().length > 0) {
            params.set("namespaceFilter", namespaceFilter.value.trim());
        }

        const kinds = getSelectedKinds();
        if (kinds.length > 0) {
            params.set("symbolKinds", kinds.join(","));
        }

        return params;
    };

    const loadGraph = async () => {
        console.log("[CodeAnalysis] loadGraph called, activeRenderer:", activeRenderer, "repositoryId:", repositoryId);

        if (activeRenderer === null || repositoryId.length === 0) {
            console.warn("[CodeAnalysis] loadGraph aborted: no renderer or repositoryId");
            return;
        }

        setPlaceholderText("Loading graph...");

        try {
            const query = buildGraphQuery();
            console.log("[CodeAnalysis] Fetching graph with query:", query.toString());
            const response = await fetch(`/api/code-analysis/graph?${query.toString()}`);

            if (!response.ok) {
                console.error("[CodeAnalysis] Graph fetch failed:", response.status);
                setPlaceholderText("Unable to load graph.");
                return;
            }

            const graphData = await response.json();
            console.log("[CodeAnalysis] Graph data received:", graphData.nodes?.length, "nodes,", graphData.edges?.length, "edges");
            const layoutName = layoutSelector instanceof HTMLSelectElement ? layoutSelector.value : "dagre";
            await activeRenderer.loadGraph(repositoryId, { graphData: graphData, layoutName: layoutName });
            setPlaceholderText("");

            if (graphPlaceholder !== null) {
                graphPlaceholder.classList.add("hidden");
            }
        } catch (error) {
            console.error("[CodeAnalysis] loadGraph error:", error);
            setPlaceholderText("Unable to load graph.");
        }
    };

    const fetchStatus = async () => {
        if (repositoryId.length === 0) {
            return null;
        }

        const response = await fetch(`/api/code-analysis/status?repositoryId=${encodeURIComponent(repositoryId)}`);
        if (!response.ok) {
            return null;
        }

        return response.json();
    };

    const updateStatusFromApi = async () => {
        const status = await fetchStatus();
        if (status === null) {
            return;
        }

        updateStatusBadge(status.status || "Unknown");

        if (status.status && status.status.toLowerCase() === "completed") {
            if (statusInterval !== null) {
                window.clearInterval(statusInterval);
                statusInterval = null;
            }

            loadGraph();
        } else if (statusInterval === null) {
            statusInterval = window.setInterval(updateStatusFromApi, 5000);
        }

        return status;
    };

    const refreshReferences = async (nodeId) => {
        if (referencesItems === null) {
            return;
        }

        referencesItems.innerHTML = "";

        try {
            const response = await fetch(`/api/code-analysis/symbols/${nodeId}/references`);
            if (!response.ok) {
                referencesItems.innerHTML = "<li class=\"text-gray-500\">No references found.</li>";
                return;
            }

            const references = await response.json();
            if (!Array.isArray(references) || references.length === 0) {
                referencesItems.innerHTML = "<li class=\"text-gray-500\">No references found.</li>";
                return;
            }

            references.forEach((reference) => {
                const listItem = document.createElement("li");
                const location = `${reference.filePath || "Unknown file"}:${reference.line || "?"}`;
                listItem.textContent = location;
                referencesItems.appendChild(listItem);
            });
        } catch (error) {
            referencesItems.innerHTML = "<li class=\"text-gray-500\">Unable to load references.</li>";
        }
    };

    const loadSourcePreview = async (filePath, lineNumber) => {
        if (!sourceCode || !sourceFile) {
            return;
        }

        if (!filePath) {
            sourceFile.textContent = "Source preview";
            sourceCode.textContent = "// Source not available.";
            return;
        }

        const startLine = lineNumber ? Math.max(1, lineNumber - 5) : 1;
        const endLine = lineNumber ? lineNumber + 5 : 200;

        try {
            const params = new URLSearchParams();
            params.set("repositoryId", repositoryId);
            params.set("filePath", filePath);
            params.set("startLine", startLine.toString());
            params.set("endLine", endLine.toString());

            const response = await fetch(`/api/code-analysis/source?${params.toString()}`);
            if (!response.ok) {
                sourceFile.textContent = filePath;
                sourceCode.textContent = "// Unable to load source preview.";
                return;
            }

            const payload = await response.json();
            sourceFile.textContent = filePath;
            if (sourceRange !== null) {
                sourceRange.textContent = lineNumber ? `Line ${lineNumber}` : "";
            }
            sourceCode.textContent = payload.content || "";

            if (window.Prism && typeof window.Prism.highlightElement === "function") {
                window.Prism.highlightElement(sourceCode);
            }
        } catch (error) {
            sourceFile.textContent = filePath;
            sourceCode.textContent = "// Unable to load source preview.";
        }
    };

    const updateSymbolDetails = (detail) => {
        if (!detail) {
            return;
        }

        if (symbolName !== null) {
            symbolName.textContent = detail.label || "Unknown symbol";
        }

        if (symbolKind !== null) {
            symbolKind.textContent = detail.kind || "Unknown";
        }

        if (symbolFullName !== null) {
            symbolFullName.textContent = detail.label || "--";
        }

        if (symbolLocation !== null) {
            if (detail.filePath) {
                const lineText = detail.line ? `:${detail.line}` : "";
                symbolLocation.textContent = `${detail.filePath}${lineText}`;
            } else {
                symbolLocation.textContent = "--";
            }
        }

        if (symbolModifiers !== null) {
            symbolModifiers.innerHTML = "";
            const badge = document.createElement("span");
            badge.className = "rounded-full border border-white/10 bg-white/5 px-2 py-1";
            badge.textContent = "Modifiers unavailable";
            symbolModifiers.appendChild(badge);
        }

        if (viewInFileLink instanceof HTMLAnchorElement) {
            viewInFileLink.href = detail.filePath ? `#${detail.filePath}` : "#";
        }
    };

    const applyNeighborFilter = (nodeId) => {
        if (activeRenderer === null) {
            return;
        }

        if (neighborToggle instanceof HTMLInputElement && neighborToggle.checked && nodeId !== null) {
            activeRenderer.showNeighborsOnly(nodeId);
        } else {
            activeRenderer.clearNeighborFocus();
        }
    };

    document.addEventListener("codeGraph:ready", (event) => {
        console.log("[CodeAnalysis] codeGraph:ready event received", event.detail);
        if (event.detail && event.detail.renderer) {
            activeRenderer = event.detail.renderer;
        }

        if (activeRenderer !== null && repositoryId.length > 0) {
            loadGraph();
            updateStatusFromApi();
        }
    });

    // Check if renderer was already initialized before this script loaded
    if (root.codeGraphRenderer) {
        console.log("[CodeAnalysis] Renderer already exists on root, using it");
        activeRenderer = root.codeGraphRenderer;
        if (repositoryId.length > 0) {
            loadGraph();
            updateStatusFromApi();
        }
    }

    document.addEventListener("codeGraph:nodeSelected", (event) => {
        if (event.detail && event.detail.nodeId) {
            lastSelectedNodeId = event.detail.nodeId;
        }

        updateSymbolDetails(event.detail);
        applyNeighborFilter(lastSelectedNodeId);

        if (event.detail && event.detail.nodeId) {
            refreshReferences(event.detail.nodeId);
        }
    });

    document.addEventListener("codeGraph:navigateToSource", (event) => {
        if (event.detail) {
            loadSourcePreview(event.detail.filePath, event.detail.line);
        }
    });

    document.addEventListener("codeGraph:graphLoaded", (event) => {
        if (event.detail && graphPlaceholder !== null) {
            if (event.detail.nodes > 0) {
                graphPlaceholder.classList.add("hidden");
            } else {
                graphPlaceholder.classList.remove("hidden");
            }
        }
    });

    if (layoutSelector instanceof HTMLSelectElement) {
        layoutSelector.addEventListener("change", () => {
            if (activeRenderer !== null) {
                activeRenderer.applyLayout(layoutSelector.value);
            }
        });
    }

    if (fitButton !== null) {
        fitButton.addEventListener("click", () => {
            if (activeRenderer !== null) {
                activeRenderer.fitToView();
            }
        });
    }

    if (exportButton !== null) {
        exportButton.addEventListener("click", () => {
            if (activeRenderer === null) {
                return;
            }

            const dataUrl = activeRenderer.exportAsImage("png");
            if (!dataUrl) {
                return;
            }

            const link = document.createElement("a");
            link.href = dataUrl;
            link.download = "code-graph.png";
            link.click();
        });
    }

    if (searchInput instanceof HTMLInputElement) {
        searchInput.addEventListener("input", () => {
            if (searchTimeout !== null) {
                window.clearTimeout(searchTimeout);
            }

            searchTimeout = window.setTimeout(() => {
                if (activeRenderer !== null) {
                    activeRenderer.search(searchInput.value);
                }
            }, 250);
        });
    }

    filterKinds.forEach((checkbox) => {
        checkbox.addEventListener("change", () => {
            if (activeRenderer !== null) {
                activeRenderer.filterByKind(getSelectedKinds());
            }

            loadGraph();
        });
    });

    if (namespaceFilter instanceof HTMLInputElement) {
        namespaceFilter.addEventListener("change", () => {
            loadGraph();
        });
    }

    if (depthSlider instanceof HTMLInputElement) {
        depthSlider.addEventListener("input", () => {
            if (depthValue !== null) {
                depthValue.textContent = depthSlider.value;
            }
        });

        depthSlider.addEventListener("change", () => {
            loadGraph();
        });
    }

    if (neighborToggle instanceof HTMLInputElement) {
        neighborToggle.addEventListener("change", () => {
            applyNeighborFilter(lastSelectedNodeId);
        });
    }

    if (startIndexingButton !== null) {
        startIndexingButton.addEventListener("click", async () => {
            if (repositoryId.length === 0) {
                updateStatusBadge("Repository required");
                return;
            }

            startIndexingButton.setAttribute("disabled", "disabled");
            updateStatusBadge("Queued");
            setPlaceholderText("Indexing repository...");

            try {
                const response = await fetch("/api/code-analysis/index", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json"
                    },
                    body: JSON.stringify({ repositoryId: repositoryId })
                });

                if (!response.ok) {
                    updateStatusBadge("Failed");
                    setPlaceholderText("Failed to queue indexing.");
                    startIndexingButton.removeAttribute("disabled");
                    return;
                }

                updateStatusBadge("Queued");

                if (statusInterval === null) {
                    statusInterval = window.setInterval(updateStatusFromApi, 5000);
                }
            } catch (error) {
                updateStatusBadge("Failed");
                setPlaceholderText("Failed to queue indexing.");
            } finally {
                startIndexingButton.removeAttribute("disabled");
            }
        });
    }

    if (repositorySelector !== null) {
        repositorySelector.addEventListener("change", onRepositoryChange);
    }

    loadRepositories();
};

document.addEventListener("DOMContentLoaded", () => {
    initializeCodeAnalysisPage();
});
