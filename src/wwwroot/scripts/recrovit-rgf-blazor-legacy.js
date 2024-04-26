/*!
* recrovit-rgf-legacy.js v1.0.1
*/

window.Recrovit = window.Recrovit || {};
window.Recrovit.RGF = window.Recrovit.RGF || {};
window.Recrovit.RGF.Blazor = window.Recrovit.RGF.Blazor || {};
var Blazor = window.Recrovit.RGF.Blazor;

Blazor.Legacy = {
    CreateRecroGridAsync: async function (entityName, containerId, dotNetRef, filter) {
        var rgServiceParams = {
            Data: { Name: entityName },
            Filter: filter
        };
        await Recrovit.WebCli.RecroGrid.CreateRecroGridAsync(rgServiceParams, `#${containerId}`, null, null, dotNetRef);
        BlazorLegacy.ChkVersion(entityName, containerId);
    },
    CreateRecroSecAsync: async function (containerId, dotNetRef) {
        var names = ['RS_ObjectPermission', 'RS_PermissionGroup', 'RS_RoleRolePriority', 'RS_vRecroSec_User', 'RS_PermissionType'];
        for (var i = 0; i < names.length; i++) {
            await BlazorLegacy.CreateRecroGridAsync(names[i], containerId, dotNetRef);
        }
    },
    ChkVersion: function (entityName, containerId) {
        if (entityName.includes("RecroGrid_Entity")) {
            $("#" + containerId).before('<div class="recrogrid-version alert alert-info" role="alert"></div>');
            Recrovit.WebCli.RecroGrid.VersionInfo('div.recrogrid-version');
        }
    }
};

const BlazorLegacy = Blazor.Legacy;