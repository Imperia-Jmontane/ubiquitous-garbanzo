class CodeGraphRenderer {
    constructor(containerId, options) {
        this.containerId = containerId;
        this.options = options || {};
        this.cy = null;
    }

    async initialize() {
        const container = document.getElementById(this.containerId);
        console.log("[CodeGraphRenderer] initialize called, container:", container, "containerId:", this.containerId);

        if (container === null) {
            console.error("[CodeGraphRenderer] Container not found");
            return;
        }

        if (typeof window.cytoscape !== "function") {
            console.error("[CodeGraphRenderer] Cytoscape not loaded");
            this.dispatchEvent("codeGraph:error", { message: "Cytoscape failed to load." });
            return;
        }

        console.log("[CodeGraphRenderer] Creating cytoscape instance");
        this.cy = window.cytoscape({
            container: container,
            elements: [],
            style: this.getStyles(),
            layout: {
                name: "dagre"
            }
        });

        this.setupEventHandlers();
        console.log("[CodeGraphRenderer] Dispatching codeGraph:ready event");
        this.dispatchEvent("codeGraph:ready", { renderer: this });
        this.dispatchEvent("codeGraph:graphLoaded", { nodes: 0, edges: 0 });
    }

    getStyles() {
        return [
            {
                selector: "node",
                style: {
                    "background-color": "#6366f1",
                    "border-width": 1,
                    "border-color": "#312e81",
                    "color": "#f9fafb",
                    "font-size": 10,
                    "label": "data(label)",
                    "text-wrap": "wrap",
                    "text-max-width": 120
                }
            },
            {
                selector: "edge",
                style: {
                    "curve-style": "bezier",
                    "line-color": "#4b5563",
                    "target-arrow-color": "#4b5563",
                    "target-arrow-shape": "triangle",
                    "width": 1
                }
            },
            {
                selector: ".is-highlighted",
                style: {
                    "background-color": "#22d3ee",
                    "line-color": "#22d3ee",
                    "target-arrow-color": "#22d3ee"
                }
            },
            {
                selector: ".is-muted",
                style: {
                    "opacity": 0.2
                }
            }
        ];
    }

    setupEventHandlers() {
        if (this.cy === null) {
            return;
        }

        this.cy.on("tap", "node", (event) => {
            const node = event.target;
            this.dispatchEvent("codeGraph:nodeSelected", {
                nodeId: node.id(),
                label: node.data("label"),
                kind: node.data("kind"),
                filePath: node.data("filePath"),
                line: node.data("line"),
                column: node.data("column")
            });
        });

        this.cy.on("dbltap", "node", (event) => {
            const node = event.target;
            this.dispatchEvent("codeGraph:navigateToSource", {
                nodeId: node.id(),
                label: node.data("label"),
                kind: node.data("kind"),
                filePath: node.data("filePath"),
                line: node.data("line"),
                column: node.data("column")
            });
        });
    }

    async loadGraph(repositoryId, options) {
        if (this.cy === null) {
            return;
        }

        if (options && options.graphData) {
            const elements = this.transformToElements(options.graphData);
            this.cy.elements().remove();
            this.cy.add(elements);
            this.applyLayout(options.layoutName || "dagre");
            this.dispatchEvent("codeGraph:graphLoaded", { nodes: elements.nodes.length, edges: elements.edges.length });
            return;
        }

        this.dispatchEvent("codeGraph:graphLoaded", { nodes: 0, edges: 0, repositoryId: repositoryId });
    }

    transformToElements(graphData) {
        const nodes = (graphData.nodes || []).map((node) => ({
            data: {
                id: node.id.toString(),
                label: node.displayName || node.serializedName,
                kind: node.type,
                parent: node.parentId ? node.parentId.toString() : undefined,
                filePath: node.filePath,
                line: node.line,
                column: node.column
            }
        }));

        const edges = (graphData.edges || []).map((edge) => ({
            data: {
                id: edge.id.toString(),
                source: edge.sourceNodeId.toString(),
                target: edge.targetNodeId.toString(),
                kind: edge.type
            }
        }));

        return { nodes: nodes, edges: edges };
    }

    applyLayout(layoutName) {
        if (this.cy === null) {
            return;
        }

        const layout = this.cy.layout({
            name: layoutName || "dagre",
            fit: true,
            padding: 30
        });

        layout.run();
    }

    fitToView() {
        if (this.cy === null) {
            return;
        }

        this.cy.fit(undefined, 30);
    }

    getNodeData(nodeId) {
        if (this.cy === null) {
            return null;
        }

        const node = this.cy.getElementById(nodeId.toString());

        if (node.empty()) {
            return null;
        }

        return node.data();
    }

    focusOnNode(nodeId) {
        if (this.cy === null) {
            return;
        }

        const node = this.cy.getElementById(nodeId.toString());

        if (node.empty() === false) {
            this.cy.animate({
                center: { eles: node },
                zoom: 1.1,
                duration: 300
            });
        }
    }

    highlightPath(sourceId, targetId) {
        if (this.cy === null) {
            return;
        }

        this.cy.elements().removeClass("is-highlighted");
        const source = this.cy.getElementById(sourceId.toString());
        const target = this.cy.getElementById(targetId.toString());

        if (source.empty() || target.empty()) {
            return;
        }

        source.addClass("is-highlighted");
        target.addClass("is-highlighted");
    }

    showNeighborsOnly(nodeId) {
        if (this.cy === null) {
            return;
        }

        this.cy.elements().removeClass("is-muted");
        const focusNode = this.cy.getElementById(nodeId.toString());

        if (focusNode.empty()) {
            return;
        }

        const neighborhood = focusNode.closedNeighborhood();
        this.cy.elements().difference(neighborhood).addClass("is-muted");
    }

    clearNeighborFocus() {
        if (this.cy === null) {
            return;
        }

        this.cy.elements().removeClass("is-muted");
    }

    filterByKind(kinds) {
        if (this.cy === null) {
            return;
        }

        const allowedKinds = new Set(kinds || []);

        this.cy.nodes().forEach((node) => {
            const kind = node.data("kind");
            const shouldShow = allowedKinds.size === 0 || allowedKinds.has(kind);
            node.style("display", shouldShow ? "element" : "none");
        });

        this.dispatchEvent("codeGraph:filterChanged", { kinds: Array.from(allowedKinds) });
    }

    search(query) {
        if (this.cy === null) {
            return;
        }

        const normalizedQuery = (query || "").toLowerCase();

        this.cy.nodes().forEach((node) => {
            const label = (node.data("label") || "").toLowerCase();
            const isMatch = normalizedQuery.length === 0 || label.includes(normalizedQuery);
            node.style("opacity", isMatch ? 1 : 0.2);
        });
    }

    exportAsImage(format) {
        if (this.cy === null) {
            return null;
        }

        const exportFormat = format || "png";

        if (exportFormat === "svg") {
            return this.cy.svg({ scale: 1, full: true });
        }

        return this.cy.png({ scale: 1, full: true });
    }

    destroy() {
        if (this.cy !== null) {
            this.cy.destroy();
            this.cy = null;
        }
    }

    dispatchEvent(name, detail) {
        const event = new CustomEvent(name, { detail: detail });
        document.dispatchEvent(event);
    }
}

const initializeCodeGraphRenderer = () => {
    const root = document.querySelector("[data-code-analysis-root]");

    if (root === null) {
        return;
    }

    const graphContainer = root.querySelector("[data-graph-container]");

    if (graphContainer === null) {
        return;
    }

    if (graphContainer.id.length === 0) {
        graphContainer.id = "code-analysis-graph";
    }

    const renderer = new CodeGraphRenderer(graphContainer.id, {});
    renderer.initialize();

    root.codeGraphRenderer = renderer;
    root.dataset.graphRendererReady = "true";
};

document.addEventListener("DOMContentLoaded", () => {
    initializeCodeGraphRenderer();
});
