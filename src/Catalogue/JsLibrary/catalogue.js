var MarkdownIt = require("markdown-it");
var hljs = require('./node_modules/highlightjs/highlight.pack.js');
//var Prism = require('./prism.js');

//let langs = hljs.listLanguages();
//for (i = 0; i < langs.length; i++) {
//    Console.WriteLine(langs[i]);
//}

//for (var propName in Prism.languages) {
//    Console.WriteLine(propName + Prism.languages[propName]);
//}

hljs.registerLanguage("flexsearch", function (a) {
    var VARIABLE = {
        className: 'variable',
        begin: /\([a-zA-Z]+/
    };
    var CONSTANT = {
        className: 'constant',
        begin: /@[a-zA-Z]+/
    };
    var SWITCH = {
        className: 'command',
        begin: /-[a-zA-Z]+/
    };
    var APOS_STRING = {
        className: 'string',
        begin: /'/, end: /'/
    };
    var FUNCTIONS = {
        className: 'function',
        begin: /[a-zA-Z]/
    }
    return {
        case_insensitive: true,
        aliases: ['flex'],
        keywords: {
            keyword: 'and or',
            ////built_in: 'anyof allof pharasematch fuzzy like regex matchall matchnone gt ge lt le',
            //literal: '-matchall -matchnone -matchFieldDefault -useDefault -boost -constantScore -noScore'
        },
        contains: [
          VARIABLE,
          CONSTANT,
          APOS_STRING,
          SWITCH
          //FUNCTIONS
          //FIELDNAME
        ]
    }
});
function highlightUsingHighlightJs (str, lang) {
    if (lang && 0 !== lang.length) {
        if (hljs.getLanguage(lang)) {
            try {
                return hljs.highlight(lang, str, true).value;
            } catch (err) {
                Console.WriteLine("hljs encountered an error: " + err);
            }
        } else {
            Console.WriteLine("hljs does not contain the definition of language: " + lang);
        }
    } else {
        Console.WriteLine("No valid language value passed to hljs.");
    }
    return '';
}
//function highlightUsingPrismJs (str, lang) {
//    if (lang && 0 !== lang.length) {
//        if (lang in Prism.languages) {
//            try {
//                return Prism.highlight(str, Prism.languages[lang]);
//            } catch (err) {
//                Console.WriteLine("PrismJS encountered an error: " + err);
//            }
//        } else {
//            Console.WriteLine("PrismJS does not contain the definition of language: " + lang);
//        }
//    } else {
//        Console.WriteLine("No valid language value passed to PrismJS.");
//    }
//    return '';
//}

var md = new MarkdownIt({
    html: true,
    linkify: true,
    typographer: true,
    langPrefix: '',
    highlight: highlightUsingHighlightJs
});
md.use(require("markdown-it-anchor"), {
    callback: anchorCallback,
    slugify: slugify
});
//md.use(require("markdown-it-table-of-contents"));
md.use(require("markdown-it-abbr"));
md.use(require("markdown-it-footnote"));
md.use(require("markdown-it-fontawesome"));
md.use(require("markdown-it-deflist"));
md.use(require("./callout.js"));

// Support for mermaid
md.use(require('markdown-it-container'), 'mermaid', {
    render: function (tokens, idx) {
        Console.WriteLine(tokens[idx].info);
        var m = tokens[idx].info.trim().match(/^mermaid\s+(.*)$/);
        if (tokens[idx].nesting === 1) {
            return '<div class="mermaid">' + tokens[idx].info.trim();
        } else {
            return '</div>\n';
        }
    }
});
window.md = md;
var calls = [];
var pageName = "";
function slugify(s) {
    return pageName + '/' +
        s.toLowerCase()
              .replace(/[^\w\s-]/g, '') // remove non-word [a-z0-9_], non-whitespace, non-hyphen characters
              .replace(/[\s_-]+/g, '-') // swap any length of whitespace, underscore, hyphen characters with a single -
              .replace(/^-+|-+$/g, ''); // remove leading, trailing -;
}

function anchorCallback(token, info) {
    calls.push({
        "Title": info.title,
        "Anchor": info.slug,
        "HeadingLevel": token.tag
    });
}

function render(name, input) {
    calls = [];
    pageName = name;
    return md.render(input);
}

function getHeadings() {
    return JSON.stringify(calls);
}

window.render = render;
window.getHeadings = getHeadings;

// A global cache for handlebar templates
var templates = {};
var handlebars = require("handlebars");

function compile(templateName, template) {
    templates[templateName] = handlebars.compile(template);
    return true;
};

function transform(templateName, data) {
    if (templates[templateName] === undefined) {
        return "Template:" + templateName + " not found.";
    }

    try {
        return templates[templateName](JSON.parse(data));
    } catch (e) {
        return "Template:" + templateName + " cannot be used. It could be due to errors in template or it is not registered correctly. Error:" + e;
    }
};

// Simple function which compiles and renders template at the same time
function compileAndTransform(templateContent, data) {
    try {
        var template = handlebars.compile(templateContent);
        return template(JSON.parse(data));
    } catch (e) {
        return "Cannot compile the template. Error:" + e;
    }
};

window.handlebars = handlebars;
window.compile = compile;
window.transform = transform;
window.compileAndTransform = compileAndTransform;

// Register Handlebar helpers
// src : http://doginthehat.com.au/2012/02/comparison-block-helper-for-handlebars-templates/#comment-44
handlebars.registerHelper('compare', function (lvalue, operator, rvalue, options) {

    var operators, result;

    if (arguments.length < 3) {
        throw new Error("Handlerbars Helper 'compare' needs 2 parameters");
    }

    if (options === undefined) {
        options = rvalue;
        rvalue = operator;
        operator = "===";
    }

    operators = {
        '==': function (l, r) { return l == r; },
        '===': function (l, r) { return l === r; },
        '!=': function (l, r) { return l != r; },
        '!==': function (l, r) { return l !== r; },
        '<': function (l, r) { return l < r; },
        '>': function (l, r) { return l > r; },
        '<=': function (l, r) { return l <= r; },
        '>=': function (l, r) { return l >= r; },
        'typeof': function (l, r) { return typeof l == r; }
    };

    if (!operators[operator]) {
        throw new Error("Handlerbars Helper 'compare' doesn't know the operator " + operator);
    }

    result = operators[operator](lvalue, rvalue);

    if (result) {
        return options.fn(this);
    } else {
        return options.inverse(this);
    }

});

handlebars.registerHelper('json', function (context) {
    return JSON.stringify(context);
});

handlebars.registerHelper('md', function (options) {
    return md.render(options.fn(this));
});

// Source : https://gist.github.com/akhoury/9118682

/* a helper to execute javascript expressions
 USAGE:
 -- Yes you NEED to properly escape the string literals or just alternate single and double quotes 
 -- to access any global function or property you should use window.functionName() instead of just functionName(), notice how I had to use window.parseInt() instead of parseInt()
 -- this example assumes you passed this context to your handlebars template( {name: 'Sam', age: '20' } )
 <p>Url: {{x " \"hi\" + this.name + \", \" + window.location.href + \" <---- this is your href,\" + " your Age is:" + window.parseInt(this.age, 10) "}}</p>
 OUTPUT:
 <p>Url: hi Sam, http://example.com <---- this is your href, your Age is: 20</p>
*/

handlebars.registerHelper("x", function (expression, options) {
    var fn = function () { }, result;
    try {
        fn = Function.apply(this, ["window", "return " + expression + " ;"]);
    } catch (e) {
        Console.WriteLine("{{x " + expression + "}} has invalid javascript", e);
    }

    try {
        result = fn.call(this, window);
    } catch (e) {
        Console.WriteLine("{{x " + expression + "}} hit a runtime error", e);
    }
    return result;
});


// for demo: http://jsbin.com/jeqesisa/7/edit
// for detailed comments, see my SO answer here http://stackoverflow.com/questions/8853396/logical-operator-in-a-handlebars-js-if-conditional/21915381#21915381

/* a helper to execute an IF statement with any expression
  USAGE:
 -- Yes you NEED to properly escape the string literals, or just alternate single and double quotes 
 -- to access any global function or property you should use window.functionName() instead of just functionName()
 -- this example assumes you passed this context to your handlebars template( {name: 'Sam', age: '20' } ), notice age is a string, just for so I can demo parseInt later
 <p>
   {{#xif " this.name == 'Sam' && this.age === '12' " }}
     BOOM
   {{else}}
     BAMM
   {{/xif}}
 </p>
 */

handlebars.registerHelper("xif", function (expression, options) {
    return handlebars.helpers["x"].apply(this, [expression, options]) ? options.fn(this) : options.inverse(this);
});

/*
Taken from: http://stackoverflow.com/questions/10377700/limit-results-of-each-in-handlebars-js
Example
{{#each_upto this 5}}
        <li>
            <p>{{tweet}}</p>
            <span id="author">{{author}}<span/>
        </li>
    {{/each_upto}}
*/
handlebars.registerHelper('each_upto', function (ary, max, options) {
    if (!ary || ary.length == 0)
        return options.inverse(this);

    var result = [];
    for (var i = 0; i < max && i < ary.length; ++i)
        result.push(options.fn(ary[i]));
    return result.join('');
});

/*
Lunr.js related 
*/
var lunr = require('lunr')
var index = lunr(function () {
    this.ref('id');
    this.field('title', { boost: 10 });
    this.field('body');
});

/// Takes all the docs as JSON and generates the index file
function createIndex(data) {
    var docs = JSON.parse(data);
    var store = {};

    docs.forEach(function (doc) {
        index.add(doc);
        store[doc.id] = { title: doc.title, href: doc.href };
    });

    return JSON.stringify({
        index: index.toJSON(),
        store: store
    });
}

window.createIndex = createIndex;

///*
//Mermaid stuff
//*/
//var mermaidAPI = require('mermaid').mermaidAPI;
//mermaidAPI.initialize({
//    startOnLoad: false
//});

//function createDiagram(id, input) {
//    var cb = function (html) {
//        return html;
//    }
//    mermaidAPI.render(id, input, cb());
//}

//window.SVGElement = {};
//window.createDiagram = createDiagram;