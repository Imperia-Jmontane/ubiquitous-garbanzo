/**
 * ============================================================================
 * REFERENCE FILE: Cytoscape.js Graph Renderer for Code Visualization
 * This is a reference implementation for the junior developer.
 * Adapt the styling and interactions to match the project's design.
 * ============================================================================
 *
 * REQUIRED DEPENDENCIES (add to your page):
 * <script src="https://unpkg.com/cytoscape@3.28.1/dist/cytoscape.min.js"></script>
 * <script src="https://unpkg.com/dagre@0.8.5/dist/dagre.min.js"></script>
 * <script src="https://unpkg.com/cytoscape-dagre@2.5.0/cytoscape-dagre.js"></script>
 *
 * HOW CYTOSCAPE WORKS:
 * 1. Create a Cytoscape instance with a container element
 * 2. Define styles for nodes and edges (like CSS but for graphs)
 * 3. Add elements (nodes and edges) with their data
 * 4. Apply a layout algorithm to position the nodes
 * 5. Handle user interactions (click, hover, etc.)
 */

class CodeGraphRenderer {
    /**
     * Creates a new CodeGraphRenderer.
     * @param {string} containerId - The ID of the HTML element to render the graph in
     * @param {Object} options - Configuration options
     */
    constructor(containerId, options = {}) {
        this.container = document.getElementById(containerId);

        if (!this.container) {
            throw new Error(`Container element not found: ${containerId}`);
        }

        // Merge default options with provided options
        this.options = {
            minZoom: 0.1,
            maxZoom: 3,
            wheelSensitivity: 0.3,
            ...options
        };

        this.cy = null;
        this.currentLayout = 'dagre';
    }

    /**
     * Initializes the Cytoscape instance.
     * Call this before loading any data.
     */
    async initialize() {
        this.cy = cytoscape({
            container: this.container,
            style: this.getStyles(),
            minZoom: this.options.minZoom,
            maxZoom: this.options.maxZoom,
            wheelSensitivity: this.options.wheelSensitivity,
            // Prevent text selection during drag
            userPanningEnabled: true,
            userZoomingEnabled: true,
            boxSelectionEnabled: true
        });

        this.setupEventHandlers();
        return this;
    }

    /**
     * Defines the visual styles for nodes and edges.
     * This is similar to CSS but uses Cytoscape's style format.
     *
     * STYLING GUIDE:
     * - 'selector' works like CSS selectors (node, edge, node[kind="class"], etc.)
     * - 'data(fieldName)' accesses data properties on elements
     * - Colors should match your app's theme (using Tailwind colors here)
     */
    getStyles() {
        return [
            // =================================================================
            // BASE NODE STYLES
            // =================================================================
            {
                selector: 'node',
                style: {
                    'label': 'data(displayName)',
                    'font-family': 'Inter, system-ui, sans-serif',
                    'text-wrap': 'wrap',
                    'text-max-width': '120px',
                    'text-valign': 'center',
                    'text-halign': 'center',
                    'background-opacity': 1,
                    'border-width': 2,
                    'overlay-padding': '6px'
                }
            },

            // =================================================================
            // NAMESPACE NODES (containers for classes)
            // Visual: Dark rounded rectangle with lighter text
            // =================================================================
            {
                selector: 'node[kind="namespace"]',
                style: {
                    'shape': 'round-rectangle',
                    'background-color': '#1e293b',  // slate-800
                    'border-color': '#475569',       // slate-600
                    'border-width': 2,
                    'font-size': '14px',
                    'color': '#94a3b8',              // slate-400
                    'text-valign': 'top',
                    'padding': '20px'
                }
            },

            // =================================================================
            // CLASS NODES
            // Visual: Blue rounded rectangle
            // =================================================================
            {
                selector: 'node[kind="class"]',
                style: {
                    'shape': 'round-rectangle',
                    'background-color': '#3b82f6',  // blue-500
                    'border-color': '#60a5fa',       // blue-400
                    'font-size': '12px',
                    'color': '#ffffff',
                    'width': 'label',
                    'height': 'label',
                    'padding': '12px'
                }
            },

            // =================================================================
            // INTERFACE NODES
            // Visual: Purple diamond shape
            // =================================================================
            {
                selector: 'node[kind="interface"]',
                style: {
                    'shape': 'diamond',
                    'background-color': '#8b5cf6',  // violet-500
                    'border-color': '#a78bfa',       // violet-400
                    'font-size': '12px',
                    'color': '#ffffff',
                    'width': 100,
                    'height': 60
                }
            },

            // =================================================================
            // STRUCT NODES
            // Visual: Teal rectangle
            // =================================================================
            {
                selector: 'node[kind="struct"]',
                style: {
                    'shape': 'rectangle',
                    'background-color': '#14b8a6',  // teal-500
                    'border-color': '#2dd4bf',       // teal-400
                    'font-size': '12px',
                    'color': '#ffffff',
                    'width': 'label',
                    'height': 'label',
                    'padding': '12px'
                }
            },

            // =================================================================
            // ENUM NODES
            // Visual: Amber hexagon
            // =================================================================
            {
                selector: 'node[kind="enum"]',
                style: {
                    'shape': 'hexagon',
                    'background-color': '#f59e0b',  // amber-500
                    'border-color': '#fbbf24',       // amber-400
                    'font-size': '12px',
                    'color': '#ffffff',
                    'width': 80,
                    'height': 70
                }
            },

            // =================================================================
            // METHOD NODES
            // Visual: Green ellipse
            // =================================================================
            {
                selector: 'node[kind="method"]',
                style: {
                    'shape': 'ellipse',
                    'background-color': '#10b981',  // emerald-500
                    'border-color': '#34d399',       // emerald-400
                    'border-width': 1,
                    'font-size': '10px',
                    'color': '#ffffff',
                    'width': 'label',
                    'height': 'label',
                    'padding': '8px'
                }
            },

            // =================================================================
            // CONSTRUCTOR NODES
            // Visual: Green ellipse with different shade
            // =================================================================
            {
                selector: 'node[kind="constructor"]',
                style: {
                    'shape': 'ellipse',
                    'background-color': '#059669',  // emerald-600
                    'border-color': '#10b981',       // emerald-500
                    'border-width': 1,
                    'font-size': '10px',
                    'color': '#ffffff',
                    'width': 'label',
                    'height': 'label',
                    'padding': '8px'
                }
            },

            // =================================================================
            // PROPERTY NODES
            // Visual: Pink rounded rectangle
            // =================================================================
            {
                selector: 'node[kind="property"]',
                style: {
                    'shape': 'round-rectangle',
                    'background-color': '#ec4899',  // pink-500
                    'border-color': '#f472b6',       // pink-400
                    'border-width': 1,
                    'font-size': '10px',
                    'color': '#ffffff',
                    'width': 'label',
                    'height': 'label',
                    'padding': '8px'
                }
            },

            // =================================================================
            // FIELD NODES
            // Visual: Orange rounded rectangle
            // =================================================================
            {
                selector: 'node[kind="field"]',
                style: {
                    'shape': 'round-rectangle',
                    'background-color': '#f97316',  // orange-500
                    'border-color': '#fb923c',       // orange-400
                    'border-width': 1,
                    'font-size': '10px',
                    'color': '#ffffff',
                    'width': 'label',
                    'height': 'label',
                    'padding': '8px'
                }
            },

            // =================================================================
            // BASE EDGE STYLES
            // =================================================================
            {
                selector: 'edge',
                style: {
                    'curve-style': 'bezier',
                    'width': 1,
                    'line-color': '#64748b',         // slate-500
                    'target-arrow-color': '#64748b',
                    'target-arrow-shape': 'vee',
                    'arrow-scale': 1
                }
            },

            // =================================================================
            // INHERITANCE EDGES (class : BaseClass)
            // Visual: Solid amber line with triangle arrow
            // =================================================================
            {
                selector: 'edge[type="inheritance"]',
                style: {
                    'target-arrow-shape': 'triangle-backcurve',
                    'target-arrow-color': '#f59e0b',  // amber-500
                    'line-color': '#f59e0b',
                    'width': 2,
                    'line-style': 'solid'
                }
            },

            // =================================================================
            // INTERFACE IMPLEMENTATION EDGES (class : IInterface)
            // Visual: Dashed purple line with triangle arrow
            // =================================================================
            {
                selector: 'edge[type="interface"]',
                style: {
                    'target-arrow-shape': 'triangle-backcurve',
                    'target-arrow-color': '#8b5cf6',  // violet-500
                    'line-color': '#8b5cf6',
                    'width': 2,
                    'line-style': 'dashed'
                }
            },

            // =================================================================
            // CALL EDGES (method invocation)
            // Visual: Thin gray line with vee arrow
            // =================================================================
            {
                selector: 'edge[type="call"]',
                style: {
                    'target-arrow-shape': 'vee',
                    'target-arrow-color': '#64748b',  // slate-500
                    'line-color': '#64748b',
                    'width': 1
                }
            },

            // =================================================================
            // TYPE USAGE EDGES (using a type)
            // Visual: Dotted line
            // =================================================================
            {
                selector: 'edge[type="typeUsage"]',
                style: {
                    'target-arrow-shape': 'none',
                    'line-color': '#94a3b8',          // slate-400
                    'width': 1,
                    'line-style': 'dotted'
                }
            },

            // =================================================================
            // CONTAINMENT EDGES (namespace contains class)
            // Visual: Light dashed line
            // =================================================================
            {
                selector: 'edge[type="contains"]',
                style: {
                    'target-arrow-shape': 'none',
                    'line-color': '#475569',          // slate-600
                    'width': 1,
                    'line-style': 'dashed',
                    'opacity': 0.5
                }
            },

            // =================================================================
            // SELECTION STATE
            // Visual: Yellow highlight
            // =================================================================
            {
                selector: ':selected',
                style: {
                    'border-width': 3,
                    'border-color': '#fbbf24',        // amber-400
                    'background-color': '#fef3c7'     // amber-100
                }
            },

            // =================================================================
            // HIGHLIGHTED STATE (for path highlighting)
            // =================================================================
            {
                selector: '.highlighted',
                style: {
                    'background-color': '#fef3c7',    // amber-100
                    'line-color': '#fbbf24',          // amber-400
                    'target-arrow-color': '#fbbf24',
                    'border-color': '#fbbf24',
                    'border-width': 3,
                    'width': 3
                }
            },

            // =================================================================
            // HOVER STATE
            // =================================================================
            {
                selector: 'node:active',
                style: {
                    'overlay-opacity': 0.2,
                    'overlay-color': '#ffffff'
                }
            },

            // =================================================================
            // FADED STATE (for dimming non-related nodes)
            // =================================================================
            {
                selector: '.faded',
                style: {
                    'opacity': 0.3
                }
            }
        ];
    }

    /**
     * Sets up event handlers for user interactions.
     */
    setupEventHandlers() {
        // Click on node to select and show details
        this.cy.on('tap', 'node', (event) => {
            const node = event.target;
            this.onNodeSelected(node.data());
        });

        // Double-click to navigate to source code
        this.cy.on('dbltap', 'node', (event) => {
            const node = event.target;
            this.navigateToSource(node.data());
        });

        // Right-click for context menu
        this.cy.on('cxttap', 'node', (event) => {
            event.preventDefault();
            const node = event.target;
            this.showContextMenu(event.originalEvent, node.data());
        });

        // Hover on edge to show tooltip
        this.cy.on('mouseover', 'edge', (event) => {
            const edge = event.target;
            this.showEdgeTooltip(event.originalEvent, edge.data());
        });

        this.cy.on('mouseout', 'edge', () => {
            this.hideEdgeTooltip();
        });

        // Click on background to deselect
        this.cy.on('tap', (event) => {
            if (event.target === this.cy) {
                // Clear highlights
                this.cy.elements().removeClass('highlighted faded');
                this.onNodeDeselected();
            }
        });
    }

    /**
     * Loads graph data from the API.
     * @param {string} repositoryId - The ID of the repository to load
     * @param {Object} options - Query options (depth, includeMembers, rootSymbol)
     */
    async loadGraph(repositoryId, options = {}) {
        const params = new URLSearchParams({
            repositoryId: repositoryId,
            depth: options.depth || 2,
            includeMembers: options.includeMembers || false,
            rootSymbol: options.rootSymbol || ''
        });

        try {
            const response = await fetch(`/api/code-analysis/graph?${params}`);

            if (!response.ok) {
                throw new Error(`Failed to load graph: ${response.statusText}`);
            }

            const graphData = await response.json();

            // Clear existing elements
            this.cy.elements().remove();

            // Transform and add new elements
            const elements = this.transformToElements(graphData);
            this.cy.add(elements);

            // Apply layout
            this.applyLayout(this.currentLayout);

            // Update stats
            this.updateStats();

            return graphData;
        } catch (error) {
            console.error('Failed to load graph:', error);
            throw error;
        }
    }

    /**
     * Transforms API response data into Cytoscape elements format.
     * @param {Object} graphData - The graph data from the API
     * @returns {Array} Array of Cytoscape elements
     */
    transformToElements(graphData) {
        const elements = [];

        // Add nodes
        for (const node of graphData.nodes) {
            elements.push({
                group: 'nodes',
                data: {
                    id: node.id.toString(),
                    displayName: node.displayName,
                    kind: this.symbolKindToString(node.type),
                    fullyQualifiedName: node.serializedName,
                    // If the node has a container, set it as parent for compound nodes
                    parent: node.containerId ? node.containerId.toString() : undefined,
                    // Source location data for navigation
                    filePath: node.filePath,
                    line: node.line,
                    column: node.column,
                    // Additional metadata
                    accessibility: node.accessibility,
                    isStatic: node.isStatic,
                    isAbstract: node.isAbstract
                }
            });
        }

        // Add edges
        for (const edge of graphData.edges) {
            elements.push({
                group: 'edges',
                data: {
                    id: `e${edge.id}`,
                    source: edge.sourceNodeId.toString(),
                    target: edge.targetNodeId.toString(),
                    type: this.referenceKindToString(edge.type)
                }
            });
        }

        return elements;
    }

    /**
     * Converts symbol kind number to string for styling.
     */
    symbolKindToString(kind) {
        const kinds = {
            1: 'namespace',
            10: 'class',
            11: 'struct',
            12: 'interface',
            13: 'enum',
            14: 'delegate',
            15: 'record',
            16: 'recordStruct',
            20: 'field',
            21: 'property',
            22: 'method',
            23: 'constructor',
            24: 'destructor',
            25: 'operator',
            26: 'indexer',
            27: 'event',
            28: 'enumMember'
        };
        return kinds[kind] || 'unknown';
    }

    /**
     * Converts reference kind number to string for styling.
     */
    referenceKindToString(kind) {
        const kinds = {
            1: 'inheritance',
            2: 'interface',
            10: 'call',
            11: 'typeUsage',
            12: 'override',
            20: 'fieldAccess',
            21: 'propertyAccess',
            22: 'eventAccess',
            30: 'contains',
            40: 'import',
            41: 'typeArgument',
            42: 'attributeUsage',
            50: 'instantiation',
            51: 'cast',
            52: 'throw',
            53: 'catch'
        };
        return kinds[kind] || 'reference';
    }

    /**
     * Applies a layout algorithm to position nodes.
     * @param {string} layoutName - The name of the layout to apply
     */
    applyLayout(layoutName) {
        this.currentLayout = layoutName;

        const layouts = {
            // Hierarchical layout (good for inheritance/call trees)
            dagre: {
                name: 'dagre',
                rankDir: 'TB',        // Top to Bottom
                nodeSep: 50,          // Horizontal spacing
                rankSep: 100,         // Vertical spacing
                edgeSep: 10,
                animate: true,
                animationDuration: 300
            },
            // Force-directed layout (good for general relationships)
            cose: {
                name: 'cose',
                idealEdgeLength: 100,
                nodeOverlap: 20,
                nodeRepulsion: 400000,
                edgeElasticity: 100,
                animate: true,
                animationDuration: 500
            },
            // Breadth-first/tree layout
            breadthfirst: {
                name: 'breadthfirst',
                directed: true,
                spacingFactor: 1.5,
                animate: true,
                animationDuration: 300
            },
            // Circular layout
            circle: {
                name: 'circle',
                spacingFactor: 1.5,
                animate: true,
                animationDuration: 300
            },
            // Grid layout
            grid: {
                name: 'grid',
                rows: undefined,       // Auto-calculate
                cols: undefined,
                animate: true,
                animationDuration: 300
            },
            // Concentric layout (nodes in circles based on degree)
            concentric: {
                name: 'concentric',
                concentric: (node) => node.degree(),
                levelWidth: () => 2,
                animate: true,
                animationDuration: 300
            }
        };

        const layout = this.cy.layout(layouts[layoutName] || layouts.dagre);
        layout.run();
    }

    /**
     * Updates the node/edge count display.
     */
    updateStats() {
        const nodeCount = this.cy.nodes().length;
        const edgeCount = this.cy.edges().length;

        const nodeCountEl = document.querySelector('[data-node-count]');
        const edgeCountEl = document.querySelector('[data-edge-count]');

        if (nodeCountEl) {
            nodeCountEl.textContent = `${nodeCount} nodes`;
        }
        if (edgeCountEl) {
            edgeCountEl.textContent = `${edgeCount} edges`;
        }
    }

    // =========================================================================
    // EVENT CALLBACKS - Override these in your implementation
    // =========================================================================

    /**
     * Called when a node is selected.
     * Override this to update your details panel.
     */
    onNodeSelected(nodeData) {
        console.log('Node selected:', nodeData);

        // Dispatch custom event for external handling
        document.dispatchEvent(new CustomEvent('codeGraph:nodeSelected', {
            detail: nodeData
        }));

        // Highlight connected elements
        this.highlightConnected(nodeData.id);
    }

    /**
     * Called when selection is cleared.
     */
    onNodeDeselected() {
        document.dispatchEvent(new CustomEvent('codeGraph:nodeDeselected'));
    }

    /**
     * Called on double-click to navigate to source code.
     */
    navigateToSource(nodeData) {
        if (nodeData.filePath && nodeData.line) {
            document.dispatchEvent(new CustomEvent('codeGraph:navigateToSource', {
                detail: {
                    filePath: nodeData.filePath,
                    line: nodeData.line,
                    column: nodeData.column
                }
            }));
        }
    }

    /**
     * Shows a context menu on right-click.
     */
    showContextMenu(event, nodeData) {
        document.dispatchEvent(new CustomEvent('codeGraph:contextMenu', {
            detail: { event, nodeData }
        }));
    }

    /**
     * Shows a tooltip for an edge.
     */
    showEdgeTooltip(event, edgeData) {
        // Implement tooltip display
        console.log('Edge hover:', edgeData);
    }

    /**
     * Hides the edge tooltip.
     */
    hideEdgeTooltip() {
        // Implement tooltip hiding
    }

    // =========================================================================
    // PUBLIC UTILITY METHODS
    // =========================================================================

    /**
     * Centers the view on a specific node.
     */
    focusOnNode(nodeId) {
        const node = this.cy.$(`#${nodeId}`);

        if (node.length > 0) {
            this.cy.animate({
                center: { eles: node },
                zoom: 2,
                duration: 300
            });
            node.select();
        }
    }

    /**
     * Highlights a node and its connected elements.
     */
    highlightConnected(nodeId) {
        const node = this.cy.$(`#${nodeId}`);

        if (node.length > 0) {
            // Get connected elements (neighbors and connecting edges)
            const connected = node.neighborhood().add(node);

            // Fade all elements
            this.cy.elements().addClass('faded');

            // Un-fade connected elements
            connected.removeClass('faded');
        }
    }

    /**
     * Highlights the shortest path between two nodes.
     */
    highlightPath(sourceId, targetId) {
        const dijkstra = this.cy.elements().dijkstra(`#${sourceId}`);
        const path = dijkstra.pathTo(`#${targetId}`);

        this.cy.elements().removeClass('highlighted faded');

        if (path.length > 0) {
            this.cy.elements().addClass('faded');
            path.removeClass('faded').addClass('highlighted');
        }
    }

    /**
     * Expands a node to show more connected elements.
     */
    async expandNode(nodeId, depth = 1) {
        const node = this.cy.$(`#${nodeId}`);

        if (node.length > 0) {
            const fullyQualifiedName = node.data('fullyQualifiedName');

            return this.loadGraph(null, {
                rootSymbol: fullyQualifiedName,
                depth: depth,
                includeMembers: true
            });
        }
    }

    /**
     * Fits the graph to the viewport.
     */
    fit() {
        this.cy.fit();
    }

    /**
     * Exports the graph as an image.
     */
    exportAsImage(format = 'png') {
        return this.cy[format]({
            output: 'blob',
            bg: '#0f172a',  // Match your dark theme background
            full: true,
            scale: 2
        });
    }

    /**
     * Filters nodes by kind.
     */
    filterByKind(kinds) {
        if (!Array.isArray(kinds) || kinds.length === 0) {
            // Show all
            this.cy.nodes().show();
        } else {
            // Hide nodes not in the list
            this.cy.nodes().forEach(node => {
                if (kinds.includes(node.data('kind'))) {
                    node.show();
                } else {
                    node.hide();
                }
            });
        }
    }

    /**
     * Searches for nodes by name.
     */
    search(query) {
        const results = [];
        const lowerQuery = query.toLowerCase();

        this.cy.nodes().forEach(node => {
            const displayName = node.data('displayName') || '';
            const fullyQualifiedName = node.data('fullyQualifiedName') || '';

            if (displayName.toLowerCase().includes(lowerQuery) ||
                fullyQualifiedName.toLowerCase().includes(lowerQuery)) {
                results.push(node.data());
            }
        });

        return results;
    }

    /**
     * Cleans up and destroys the Cytoscape instance.
     */
    destroy() {
        if (this.cy) {
            this.cy.destroy();
            this.cy = null;
        }
    }
}

// Export for use
window.CodeGraphRenderer = CodeGraphRenderer;
