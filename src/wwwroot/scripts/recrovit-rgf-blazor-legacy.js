/*!
* recrovit-rgf-legacy.js v1.0.0
*/

window.Recrovit = window.Recrovit || { };
window.Recrovit.RGF = window.Recrovit.RGF || {};
window.Recrovit.RGF.Blazor = window.Recrovit.RGF.Blazor || {};
var Blazor = window.Recrovit.RGF.Blazor;

Blazor.Legacy = {
    CreateRecroGridAsync: async function (entityName, contenerId, dotNetRef, filter) {
        var rgServiceParams = {
            Data: { Name: entityName },
            Filter: filter
        };
        await Recrovit.WebCli.RecroGrid.CreateRecroGridAsync(rgServiceParams, `#${contenerId}`, null, null, dotNetRef);
    },
    CreateRecroSecAsync: async function (contenerId, dotNetRef) {
        var names = ['RS_ObjectPermission', 'RS_PermissionGroup', 'RS_RoleRolePriority', 'RS_vRecroSec_User', 'RS_PermissionType'];
        for (var i = 0; i < names.length; i++) {
            await BlazorLegacy.CreateRecroGridAsync(names[i], contenerId, dotNetRef);
        }
    }
};

const BlazorLegacy = Blazor.Legacy;