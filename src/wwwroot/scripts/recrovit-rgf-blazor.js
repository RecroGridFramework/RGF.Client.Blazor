/*!
* recrovit-rgf-blazor.js v1.0.1
*/

window.Recrovit = window.Recrovit || { };
window.Recrovit.RGF = window.Recrovit.RGF || { };
window.Recrovit.RGF.Blazor = window.Recrovit.RGF.Blazor || {};
var Blazor = window.Recrovit.RGF.Blazor;

Blazor.Client = {
    createFn: function (funct) {
        return new Function('args', 'try {' + funct + '} catch(err) { console.error(err.message); }');
    },
    capitalizeProperties: function (obj, excludedNames = []) {
        if (typeof obj !== 'object' || obj === null) {
            return obj;
        }

        if (Array.isArray(obj)) {
            for (let i = 0; i < obj.length; i++) {
                obj[i] = this.capitalizeProperties(obj[i], excludedNames);
            }
        }
        else {
            const propertyNames = Object.getOwnPropertyNames(obj);
            propertyNames.forEach(key => {
                if (typeof obj[key] === 'object' && obj[key] !== null) {
                    obj[key] = this.capitalizeProperties(obj[key], excludedNames);
                }

                if (!excludedNames.includes(key)) {
                    const capitalizedKey = key.charAt(0).toUpperCase() + key.slice(1);
                    Object.defineProperty(obj, capitalizedKey, Object.getOwnPropertyDescriptor(obj, key));
                    //delete obj[key];

                    //    Object.defineProperty(obj, capitalizedKey, {
                    //        get() { return obj[key]; },
                    //        set(value) { obj[key] = value; },
                    //        enumerable: true,
                    //        configurable: true
                    //    });
                }
            });
        }
        return obj;
    },
    prepareGridColArg: function (args) {
        args.GetColumnDefs = function (clientName) {
            if (args.Columns[clientName] != undefined) {
                return args.Columns[clientName].ColumnDefs;
            }
        };
        args.GetValue = function (clientName) {
            if (args.Columns[clientName] != undefined) {
                return args.Columns[clientName].Value;
            }
        }
        return args;
    },
    invokeGridColFuncAsync: function (funct, args) {
        this.capitalizeProperties(args);
        return this.createFn(funct)(this.prepareGridColArg(args));
    },
    invokeGridColActionAsync: function (funct, args) {
        this.capitalizeProperties(args);
        //console.log(funct, args);
        this.createFn(funct)(this.prepareGridColArg(args));
    },
    downloadFileFromStream: async function(fileName, contentStreamReference) {
        const arrayBuffer = await contentStreamReference.arrayBuffer();
        const blob = new Blob([arrayBuffer]);
        const url = URL.createObjectURL(blob);
        const anchorElement = document.createElement('a');
        anchorElement.href = url;
        anchorElement.download = fileName ?? '';
        anchorElement.click();
        anchorElement.remove();
        URL.revokeObjectURL(url);
    }
};
