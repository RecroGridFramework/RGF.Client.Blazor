/*!
* recrovit-rgf-legacy.js v1.1.0
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
        BlazorLegacy.ChkVersion(entityName, containerId);
        await Recrovit.WebCli.RecroGrid.CreateRecroGridAsync(rgServiceParams, `#${containerId}`, null, null, dotNetRef);
    },
    CreateRecroSecAsync: async function (containerId, dotNetRef) {
        var names = ['RS_ObjectPermission', 'RS_PermissionGroup', 'RS_RoleRolePriority', 'RS_vRecroSec_User', 'RS_PermissionType'];
        for (var i = 0; i < names.length; i++) {
            await BlazorLegacy.CreateRecroGridAsync(names[i], containerId, dotNetRef);
        }
    },
    ChkVersion: function (entityName, containerId) {
        if (entityName.includes("RecroGrid_Entity") && $('div.recrogrid-version.alert').length == 0) {
            $("#" + containerId).append('<div class="recrogrid-version alert alert-info" role="alert"></div>');
            Recrovit.WebCli.RecroGrid.VersionInfo('div.recrogrid-version');
        }
    },
    Dispose: function () {
        $('.ui-dialog.recro-grid-base').remove();
    },
};

const BlazorLegacy = Blazor.Legacy;