(function () {
    const sortableSelector = "table[data-client-sort='true']";
    const directionAttribute = "data-sort-direction";
    const columnAttribute = "data-sort-column";
    const groupRowClass = "sortable-date-group-row";
    const groupedCellClass = "sortable-date-time-cell";
    const splitCellClass = "sortable-date-split-cell";
    let isSorting = false;
    let refreshTimer = null;
    let refreshFrame = null;

    function getColumnIndex(headerCell) {
        let index = 0;
        for (const cell of headerCell.parentElement.children) {
            if (cell === headerCell) {
                return index;
            }

            index += cell.colSpan || 1;
        }

        return -1;
    }

    function getHeaderCells(table) {
        const headerRow = table.tHead?.rows?.[table.tHead.rows.length - 1];
        return headerRow ? Array.from(headerRow.cells) : [];
    }

    function getHeaderCount(table) {
        return getHeaderCells(table).reduce((total, cell) => total + (cell.colSpan || 1), 0);
    }

    function isGroupRow(row) {
        return row.classList.contains(groupRowClass);
    }

    function isDetailRow(row, headerCount) {
        return !isGroupRow(row) &&
            row.cells.length === 1 &&
            row.cells[0].colSpan >= Math.max(1, headerCount);
    }

    function restoreDateCells(table) {
        for (const cell of table.querySelectorAll(`tbody .${groupedCellClass}, tbody .${splitCellClass}`)) {
            if (cell.dataset.originalText !== undefined) {
                cell.textContent = cell.dataset.originalText;
            }

            cell.classList.remove(groupedCellClass);
            cell.classList.remove(splitCellClass);
        }
    }

    function removeGroupRows(table) {
        for (const row of table.querySelectorAll(`tbody .${groupRowClass}`)) {
            row.remove();
        }

        table.classList.remove("sortable-date-grouped");
        restoreDateCells(table);
    }

    function collectRowGroups(tbody, headerCount) {
        const groups = [];

        for (const row of Array.from(tbody.rows)) {
            if (isGroupRow(row)) {
                continue;
            }

            if (isDetailRow(row, headerCount) && groups.length > 0) {
                groups[groups.length - 1].detailRows.push(row);
                continue;
            }

            groups.push({ row, detailRows: [] });
        }

        return groups;
    }

    function getTableSignature(table) {
        const rows = Array.from(table.tBodies?.[0]?.rows || [])
            .filter(row => !isGroupRow(row))
            .map(row => Array.from(row.cells).map(cell => cell.dataset.originalText || cell.innerText || "").join("|"));

        return [
            getHeaderCells(table).map(header => header.innerText || "").join("|"),
            rows.length,
            rows[0] || "",
            rows[rows.length - 1] || ""
        ].join("::");
    }

    function getCellText(row, columnIndex) {
        const cell = row.cells[columnIndex];
        const value = cell?.dataset.sortValue || cell?.dataset.originalText || cell?.innerText || "";
        return value.replace(/\s+/g, " ").trim();
    }

    function parseNumber(value) {
        const cleaned = value
            .replace(/,/g, "")
            .replace(/[^\d.\-]/g, "");

        if (!cleaned || cleaned === "-" || cleaned === "." || cleaned === "-.") {
            return null;
        }

        const parsed = Number(cleaned);
        return Number.isFinite(parsed) ? parsed : null;
    }

    function normalizeDateText(value) {
        return value
            .replace(/\./g, "-")
            .replace(/\//g, "-")
            .replace(/\s+/g, " ")
            .trim();
    }

    function parseDate(value) {
        const normalized = normalizeDateText(value);
        const dateLike = /^\d{4}-\d{1,2}-\d{1,2}(?:\s+\d{1,2}:\d{2}(?::\d{2})?)?/.test(normalized);
        if (!dateLike) {
            return null;
        }

        const parsed = Date.parse(normalized.replace(" ", "T"));
        return Number.isFinite(parsed) ? parsed : null;
    }

    function getDateGroupValue(row, columnIndex) {
        const text = normalizeDateText(getCellText(row, columnIndex));
        const match = text.match(/^(\d{4})-(\d{1,2})-(\d{1,2})/);
        if (!match) {
            return "";
        }

        return `${match[1]}-${match[2].padStart(2, "0")}-${match[3].padStart(2, "0")}`;
    }

    function getTimeValue(row, columnIndex) {
        const text = normalizeDateText(getCellText(row, columnIndex));
        const match = text.match(/\b(\d{1,2}:\d{2}(?::\d{2})?)\b/);
        return match ? match[1] : "";
    }

    function getDateValue(row, columnIndex) {
        const groupValue = getDateGroupValue(row, columnIndex);
        return groupValue ? formatDateGroupLabel(groupValue) : "";
    }

    function formatDateGroupLabel(value) {
        if (!value) {
            return "Огноо байхгүй";
        }

        const parts = value.split("-");
        if (parts.length !== 3) {
            return value;
        }

        return `${parts[0]}.${parts[1]}.${parts[2]}`;
    }

    function detectType(values) {
        const nonEmptyValues = values.filter(Boolean);
        if (nonEmptyValues.length === 0) {
            return "text";
        }

        const dateCount = nonEmptyValues.filter(value => parseDate(value) !== null).length;
        if (dateCount / nonEmptyValues.length >= 0.75) {
            return "date";
        }

        const numberCount = nonEmptyValues.filter(value => parseNumber(value) !== null).length;
        if (numberCount / nonEmptyValues.length >= 0.75) {
            return "number";
        }

        return "text";
    }

    function detectColumnType(rowGroups, columnIndex) {
        return detectType(rowGroups.map(group => getCellText(group.row, columnIndex)));
    }

    function compareValues(left, right, type, direction) {
        let result;

        if (type === "number") {
            result = (parseNumber(left) ?? Number.NEGATIVE_INFINITY) - (parseNumber(right) ?? Number.NEGATIVE_INFINITY);
        } else if (type === "date") {
            result = (parseDate(left) ?? 0) - (parseDate(right) ?? 0);
        } else {
            result = left.localeCompare(right, "mn", { sensitivity: "base", numeric: true });
        }

        return direction === "asc" ? result : -result;
    }

    function clearHeaderState(table) {
        for (const header of table.querySelectorAll("thead th")) {
            header.removeAttribute(directionAttribute);
            header.removeAttribute("aria-sort");
        }
    }

    function appendRows(tbody, rowGroups) {
        const fragment = document.createDocumentFragment();
        for (const group of rowGroups) {
            fragment.appendChild(group.row);
            for (const detailRow of group.detailRows) {
                fragment.appendChild(detailRow);
            }
        }

        tbody.appendChild(fragment);
    }

    function renderSplitDateCell(row, columnIndex) {
        const cell = row.cells[columnIndex];
        if (!cell || parseDate(getCellText(row, columnIndex)) === null) {
            return;
        }

        if (cell.dataset.originalText === undefined) {
            cell.dataset.originalText = cell.innerText.replace(/\s+/g, " ").trim();
        }

        const dateValue = getDateValue(row, columnIndex);
        const timeValue = getTimeValue(row, columnIndex);
        cell.textContent = "";

        const dateSpan = document.createElement("span");
        dateSpan.className = "sortable-date-main";
        dateSpan.textContent = dateValue || cell.dataset.originalText;
        cell.appendChild(dateSpan);

        if (timeValue) {
            const timeSpan = document.createElement("span");
            timeSpan.className = "sortable-date-sub";
            timeSpan.textContent = timeValue;
            cell.appendChild(timeSpan);
        }

        cell.classList.remove(groupedCellClass);
        cell.classList.add(splitCellClass);
    }

    function renderUngroupedDateColumns(table, rowGroups) {
        const headers = getHeaderCells(table);
        const dateColumnIndexes = [];

        for (const header of headers) {
            const columnIndex = getColumnIndex(header);
            if (columnIndex >= 0 && detectColumnType(rowGroups, columnIndex) === "date") {
                dateColumnIndexes.push(columnIndex);
            }
        }

        for (const group of rowGroups) {
            for (const columnIndex of dateColumnIndexes) {
                renderSplitDateCell(group.row, columnIndex);
            }
        }
    }

    function appendDateGroupedRows(table, tbody, rowGroups, columnIndex, headerCount) {
        const fragment = document.createDocumentFragment();
        let activeGroup = null;

        for (const group of rowGroups) {
            const groupValue = getDateGroupValue(group.row, columnIndex);
            if (groupValue !== activeGroup) {
                activeGroup = groupValue;

                const groupRow = document.createElement("tr");
                groupRow.className = groupRowClass;

                const groupCell = document.createElement("td");
                groupCell.colSpan = headerCount;
                groupCell.textContent = formatDateGroupLabel(groupValue);

                groupRow.appendChild(groupCell);
                fragment.appendChild(groupRow);
            }

            const dateCell = group.row.cells[columnIndex];
            if (dateCell) {
                if (dateCell.dataset.originalText === undefined) {
                    dateCell.dataset.originalText = dateCell.innerText.replace(/\s+/g, " ").trim();
                }

                dateCell.textContent = getTimeValue(group.row, columnIndex) || dateCell.dataset.originalText;
                dateCell.classList.add(groupedCellClass);
            }

            fragment.appendChild(group.row);
            for (const detailRow of group.detailRows) {
                fragment.appendChild(detailRow);
            }
        }

        tbody.appendChild(fragment);
        table.classList.add("sortable-date-grouped");
    }

    function sortTable(headerCell, forcedDirection) {
        const table = headerCell.closest(sortableSelector);
        const tbody = table?.tBodies?.[0];
        if (!table || !tbody || headerCell.colSpan > 1) {
            return;
        }

        const columnIndex = getColumnIndex(headerCell);
        if (columnIndex < 0) {
            return;
        }

        isSorting = true;
        try {
            const headerCount = getHeaderCount(table);
            removeGroupRows(table);

            const rowGroups = collectRowGroups(tbody, headerCount);
            const type = detectColumnType(rowGroups, columnIndex);
            const currentDirection = headerCell.getAttribute(directionAttribute);
            const defaultDirection = type === "text" ? "asc" : "desc";
            const nextDirection = forcedDirection || (currentDirection ? (currentDirection === "asc" ? "desc" : "asc") : defaultDirection);

            rowGroups.sort((left, right) => {
                const leftValue = getCellText(left.row, columnIndex);
                const rightValue = getCellText(right.row, columnIndex);
                return compareValues(leftValue, rightValue, type, nextDirection);
            });

        if (type === "date" && table.dataset.dateGroup === "true") {
            appendDateGroupedRows(table, tbody, rowGroups, columnIndex, headerCount);
        } else {
            renderUngroupedDateColumns(table, rowGroups);
            appendRows(tbody, rowGroups);
        }

            table.setAttribute(columnAttribute, String(columnIndex));
            table.classList.add("sortable-table");
            clearHeaderState(table);
            headerCell.setAttribute(directionAttribute, nextDirection);
            headerCell.setAttribute("aria-sort", nextDirection === "asc" ? "ascending" : "descending");
            table.dataset.sortSignature = getTableSignature(table);
        } finally {
            isSorting = false;
        }
    }

    function findDefaultDateHeader(table) {
        const tbody = table.tBodies?.[0];
        if (!tbody) {
            return null;
        }

        const headerCount = getHeaderCount(table);
        const rowGroups = collectRowGroups(tbody, headerCount);
        const headers = getHeaderCells(table);

        for (const header of headers) {
            if (header.colSpan > 1 || header.querySelector("button, a, input, select, textarea")) {
                continue;
            }

            const columnIndex = getColumnIndex(header);
            if (columnIndex >= 0 && detectColumnType(rowGroups, columnIndex) === "date") {
                return header;
            }
        }

        return null;
    }

    function prepareTables(root) {
        for (const table of root.querySelectorAll(sortableSelector)) {
            if (!table.tHead || !table.tBodies.length) {
                continue;
            }

            table.classList.add("sortable-table");
            const signature = getTableSignature(table);
            if (table.dataset.sortSignature && table.dataset.sortSignature !== signature) {
                table.dataset.defaultSortApplied = "false";
                removeGroupRows(table);
                clearHeaderState(table);
            }

            for (const header of table.querySelectorAll("thead th")) {
                if (header.colSpan > 1 ||
                    header.querySelector("button, a, input, select, textarea") ||
                    !header.innerText.trim()) {
                    continue;
                }

                header.tabIndex = 0;
                header.setAttribute("role", "button");
                header.setAttribute("title", "Энэ баганаар эрэмбэлэх");
            }

            if (table.dataset.dateGroup !== "true") {
                table.dataset.sortSignature = signature;
                continue;
            }

            if (table.dataset.defaultSortApplied === "true") {
                table.dataset.sortSignature = signature;
                continue;
            }

            const defaultHeader = findDefaultDateHeader(table);
            if (!defaultHeader) {
                continue;
            }

            table.dataset.defaultSortApplied = "true";
            sortTable(defaultHeader, "desc");
            table.dataset.sortSignature = getTableSignature(table);
        }
    }

    function scheduleRefresh() {
        if (isSorting) {
            return;
        }

        window.clearTimeout(refreshTimer);
        if (refreshFrame !== null) {
            window.cancelAnimationFrame(refreshFrame);
            refreshFrame = null;
        }

        refreshTimer = window.setTimeout(function () {
            refreshFrame = window.requestAnimationFrame(function () {
                refreshFrame = window.requestAnimationFrame(function () {
                    refreshFrame = null;
                    prepareTables(document);
                });
            });
        }, 220);
    }

    function hasTableMutation(mutations) {
        for (const mutation of mutations) {
            if (mutation.target?.nodeType === Node.ELEMENT_NODE &&
                mutation.target.closest?.(sortableSelector)) {
                return true;
            }

            for (const node of mutation.addedNodes) {
                if (node.nodeType !== Node.ELEMENT_NODE) {
                    continue;
                }

                if (node.matches?.(sortableSelector) ||
                    node.closest?.(sortableSelector) ||
                    node.querySelector?.(sortableSelector)) {
                    return true;
                }
            }
        }

        return false;
    }

    document.addEventListener("click", function (event) {
        const headerCell = event.target.closest(`${sortableSelector} thead th`);
        if (!headerCell || headerCell.querySelector("button, a, input, select, textarea")) {
            return;
        }

        sortTable(headerCell);
    });

    document.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" && event.key !== " ") {
            return;
        }

        const headerCell = event.target.closest(`${sortableSelector} thead th`);
        if (!headerCell) {
            return;
        }

        event.preventDefault();
        sortTable(headerCell);
    });

    document.addEventListener("DOMContentLoaded", function () {
        prepareTables(document);
    });

    document.addEventListener("enhancedload", function () {
        prepareTables(document);
    });

    document.addEventListener("blazor-enhanced-load", function () {
        prepareTables(document);
    });

    const observer = new MutationObserver(function (mutations) {
        if (hasTableMutation(mutations)) {
            scheduleRefresh();
        }
    });

    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
    } else {
        document.addEventListener("DOMContentLoaded", function () {
            observer.observe(document.body, { childList: true, subtree: true });
        });
    }

    window.bankTableSorter = {
        refresh: function () {
            prepareTables(document);
        }
    };
})();
