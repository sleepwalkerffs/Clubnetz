// Endpoint reference of the API docs: loads the OpenAPI document of Clubnetz and renders it as a searchable list.
// Plain JavaScript without dependencies. Everything from the document is inserted as text, never as HTML.
(function () {
    'use strict';

    var root = document.getElementById('api-reference');
    if (!root) return;

    var text = root.dataset;
    var clubPrefix = '/api/Clubs/{clubId}';
    var methods = ['get', 'post', 'put', 'patch', 'delete'];
    var maxDepth = 6;

    function el(tag, className, content) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (content !== undefined && content !== null) node.textContent = content;
        return node;
    }

    // Local preview only: lets a developer point the page at a locally running Clubnetz (?openapi=http://localhost:5081/...).
    function documentUrl() {
        var isLocal = ['localhost', '127.0.0.1'].indexOf(location.hostname) !== -1;
        var override = isLocal ? new URLSearchParams(location.search).get('openapi') : null;
        return override || text.openapi;
    }

    function resolve(doc, schema) {
        var guard = 0;
        while (schema && schema.$ref && guard++ < 10) {
            var name = schema.$ref.split('/').pop();
            schema = doc.components && doc.components.schemas ? doc.components.schemas[name] : null;
        }
        return schema;
    }

    function nullable(schema, textValue) {
        return schema.nullable ? textValue + ' | null' : textValue;
    }

    function comment(schema) {
        if (!schema.description) return '';
        var description = schema.description.replace(/\s+/g, ' ').trim();
        return '  // ' + (description.length > 140 ? description.slice(0, 139) + '…' : description);
    }

    // A compact, JSON-like description of a schema: property names with their types.
    function describe(doc, schema, indent, seen) {
        var ref = schema && schema.$ref;
        schema = resolve(doc, schema);
        if (!schema) return 'any';

        if (schema.allOf && schema.allOf.length === 1) return describe(doc, schema.allOf[0], indent, seen);
        if (schema.oneOf || schema.anyOf) {
            return (schema.oneOf || schema.anyOf).map(function (s) { return describe(doc, s, indent, seen); }).join(' | ');
        }

        if (schema.enum) {
            return nullable(schema, schema.enum.map(function (v) { return JSON.stringify(v); }).join(' | '));
        }

        var type = Array.isArray(schema.type) ? schema.type.filter(function (t) { return t !== 'null'; })[0] : schema.type;

        if (type === 'array') {
            return nullable(schema, '[ ' + describe(doc, schema.items || {}, indent, seen) + ' ]');
        }

        if (type === 'object' || schema.properties) {
            if (ref && seen.indexOf(ref) !== -1 || indent.length / 2 >= maxDepth) return nullable(schema, '{ … }');
            var nextSeen = ref ? seen.concat(ref) : seen;
            var names = Object.keys(schema.properties || {});
            if (names.length === 0) return nullable(schema, schema.additionalProperties ? '{ [key]: ' + describe(doc, schema.additionalProperties, indent, nextSeen) + ' }' : '{ }');

            var inner = indent + '  ';
            var lines = names.map(function (name, index) {
                var property = schema.properties[name];
                var line = inner + '"' + name + '": ' + describe(doc, property, inner, nextSeen) + (index < names.length - 1 ? ',' : '');
                return line + comment(property);
            });
            return nullable(schema, '{\n' + lines.join('\n') + '\n' + indent + '}');
        }

        if (type === 'string') {
            var formats = { date: 'date (YYYY-MM-DD)', 'date-time': 'date-time (ISO 8601)', time: 'time (HH:mm:ss)', 'date-span': 'time (HH:mm:ss)', binary: 'file' };
            return nullable(schema, formats[schema.format] || 'string');
        }
        if (type === 'integer') return nullable(schema, 'integer');
        if (type === 'number') return nullable(schema, 'number');
        if (type === 'boolean') return nullable(schema, 'boolean');
        return nullable(schema, type || 'any');
    }

    function shortType(doc, schema) {
        var value = describe(doc, schema || {}, '', []);
        return value.indexOf('\n') === -1 ? value : 'object';
    }

    function contentSchema(content) {
        if (!content) return null;
        var type = content['application/json'] ? 'application/json' : Object.keys(content)[0];
        return type ? { type: type, schema: content[type].schema } : null;
    }

    function heading(label) {
        return el('h4', 'api-ref__label', label);
    }

    function parameters(doc, operation, pathItem) {
        var list = (pathItem.parameters || []).concat(operation.parameters || []).filter(function (p) { return p.name !== 'clubId'; });
        if (list.length === 0) return null;

        var wrapper = el('div', 'docs__table');
        var table = el('table');
        var head = el('tr');
        [text.name, text.in, text.type, text.description].forEach(function (label) { head.appendChild(el('th', null, label)); });
        var thead = el('thead');
        thead.appendChild(head);
        table.appendChild(thead);

        var body = el('tbody');
        list.forEach(function (parameter) {
            var row = el('tr');
            var name = el('td');
            name.appendChild(el('code', null, parameter.name));
            if (parameter.required) name.appendChild(el('span', 'api-ref__required', ' ' + text.required));
            row.appendChild(name);
            row.appendChild(el('td', null, parameter.in));
            var type = el('td');
            type.appendChild(el('code', null, shortType(doc, parameter.schema)));
            row.appendChild(type);
            row.appendChild(el('td', null, parameter.description || ''));
            body.appendChild(row);
        });
        table.appendChild(body);
        wrapper.appendChild(table);

        var fragment = document.createDocumentFragment();
        fragment.appendChild(heading(text.parameters));
        fragment.appendChild(wrapper);
        return fragment;
    }

    function schemaBlock(doc, label, content) {
        var fragment = document.createDocumentFragment();
        fragment.appendChild(heading(label));

        if (!content || !content.schema) {
            fragment.appendChild(el('p', 'docs__muted', text.noContent));
            return fragment;
        }

        var isJson = content.type.indexOf('json') !== -1;
        var body = isJson ? describe(doc, content.schema, '', []) : text.file + ' (' + content.type + ')';
        var pre = el('pre');
        pre.appendChild(el('code', null, body));
        fragment.appendChild(pre);
        return fragment;
    }

    function details(doc, operation, pathItem) {
        var box = el('div', 'api-ref__body');
        if (operation.description) box.appendChild(el('p', null, operation.description));

        var params = parameters(doc, operation, pathItem);
        if (params) box.appendChild(params);

        if (operation.requestBody) box.appendChild(schemaBlock(doc, text.body, contentSchema(operation.requestBody.content)));

        var responses = operation.responses || {};
        var success = responses['200'] || responses['201'] || responses['204'];
        box.appendChild(schemaBlock(doc, text.response, success ? contentSchema(success.content) : null));
        return box;
    }

    function operationNode(doc, method, path, operation, pathItem) {
        var node = el('details', 'api-ref__operation');
        var shortPath = path.indexOf(clubPrefix) === 0 ? path.slice(clubPrefix.length) || '/' : path;
        node.dataset.search = (method + ' ' + shortPath + ' ' + (operation.summary || '') + ' ' + (operation.tags || []).join(' ')).toLowerCase();

        var summary = el('summary');
        summary.appendChild(el('span', 'api-ref__method api-ref__method--' + method, method.toUpperCase()));
        summary.appendChild(el('code', 'api-ref__path', shortPath));
        summary.appendChild(el('span', 'api-ref__summary', operation.summary || ''));
        node.appendChild(summary);

        // The details are only built when an endpoint is opened, the document describes more than a hundred of them
        node.addEventListener('toggle', function () {
            if (node.open && !node.querySelector('.api-ref__body')) {
                var body = details(doc, operation, pathItem);
                if (method !== 'get') body.insertBefore(el('p', 'api-ref__write', '✏️ ' + text.write), body.firstChild);
                node.appendChild(body);
            }
        });
        return node;
    }

    function render(doc) {
        var descriptions = {};
        (doc.tags || []).forEach(function (tag) { descriptions[tag.name] = tag.description; });

        var groups = {};
        var total = 0;
        Object.keys(doc.paths || {}).sort().forEach(function (path) {
            var pathItem = doc.paths[path];
            methods.forEach(function (method) {
                var operation = pathItem[method];
                if (!operation) return;
                var tag = (operation.tags && operation.tags[0]) || 'Other';
                (groups[tag] = groups[tag] || []).push(operationNode(doc, method, path, operation, pathItem));
                total++;
            });
        });

        root.textContent = '';

        var tools = el('div', 'api-ref__tools');
        var input = el('input', 'api-ref__filter');
        input.type = 'search';
        input.placeholder = text.filter;
        input.setAttribute('aria-label', text.filter);
        var count = el('span', 'api-ref__count', text.count.replace('{0}', total));
        tools.appendChild(input);
        tools.appendChild(count);
        root.appendChild(tools);

        var empty = el('p', 'docs__muted', text.empty);
        empty.hidden = true;

        var sections = Object.keys(groups).sort().map(function (tag) {
            var section = el('section', 'api-ref__group');
            section.appendChild(el('h3', null, tag));
            if (descriptions[tag]) section.appendChild(el('p', 'docs__muted', descriptions[tag].replace(/\s+/g, ' ')));
            groups[tag].forEach(function (node) { section.appendChild(node); });
            root.appendChild(section);
            return section;
        });
        root.appendChild(empty);

        input.addEventListener('input', function () {
            var terms = input.value.toLowerCase().split(/\s+/).filter(Boolean);
            var visible = 0;
            sections.forEach(function (section) {
                var shown = 0;
                section.querySelectorAll('.api-ref__operation').forEach(function (node) {
                    var match = terms.every(function (term) { return node.dataset.search.indexOf(term) !== -1; });
                    node.hidden = !match;
                    if (match) shown++;
                });
                section.hidden = shown === 0;
                visible += shown;
            });
            empty.hidden = visible !== 0;
            count.textContent = text.count.replace('{0}', visible);
        });
    }

    root.appendChild(el('p', 'docs__muted', text.loading));

    fetch(documentUrl(), { credentials: 'omit' })
        .then(function (response) {
            if (!response.ok) throw new Error('HTTP ' + response.status);
            return response.json();
        })
        .then(render)
        .catch(function () {
            root.textContent = '';
            root.appendChild(el('p', 'api-ref__error', text.failed));
        });
})();
