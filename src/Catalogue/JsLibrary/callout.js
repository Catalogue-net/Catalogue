/*
https://github.com/nunof07/markdown-it-alerts/blob/master/LICENSE

The MIT License (MIT)

Copyright (c) 2015 Nuno Freitas

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

'use strict';

var container = require('markdown-it-container');

module.exports = function callout_plugin(md, options) {
    var containerOpenCount = 0;
    var links = options ? options.links : true;
    init();
    return;

    function setupContainerCallouts(name) {
        md.use(container, name, {
            render: function (tokens, idx) {
                if (tokens[idx].nesting === 1) {
                    containerOpenCount += 1;
                    return '<div class="callout callout-' + name + '">\n';
                } else {
                    containerOpenCount -= 1;
                    return '</div>\n';
                }
            }
        });
    }

    function setupContainer(name) {
        md.use(container, name, {
            render: function (tokens, idx) {
                if (tokens[idx].nesting === 1) {
                    containerOpenCount += 1;
                    return '<div class="alert ' + name + '" role="alert">\n';
                } else {
                    containerOpenCount -= 1;
                    return '</div>\n';
                }
            }
        });
    }

    function isContainerOpen() {
        return containerOpenCount > 0;
    }

    function selfRender(tokens, idx, options, env, self) {
        return self.renderToken(tokens, idx, options);
    }

    function setupLinks() {
        var defaultRender = md.renderer.rules.link_open || selfRender;

        md.renderer.rules.link_open = function (tokens, idx, options, env, self) {
            if (isContainerOpen()) {
                tokens[idx].attrPush(['class', 'alert-link']);
            }

            return defaultRender(tokens, idx, options, env, self);
        };
    }

    function init() {
        setupContainerCallouts('success');
        setupContainerCallouts('info');
        setupContainerCallouts('warning');
        setupContainerCallouts('danger');
        setupContainer('alert-success');
        setupContainer('alert-info');
        setupContainer('alert-warning');
        setupContainer('alert-danger');

        if (links) {
            setupLinks();
        }
    }
};
