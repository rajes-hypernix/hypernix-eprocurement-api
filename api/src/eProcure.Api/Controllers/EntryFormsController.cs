using eProcure.Api.Auth;
using eProcure.Application.Authorization;
using eProcure.Application.Forms;
using Microsoft.AspNetCore.Mvc;

namespace eProcure.Api.Controllers;

/// <summary>
/// Entry-form DEFINITIONS — Admin Setup (AUTHORIZATION-MATRIX A69). "Entry forms" carry
/// record-entry BEHAVIOUR (fields/groups/subtabs/display/required/defaults) — distinct
/// from /api/forms (FormTemplate = RFQ bid questionnaires).
/// </summary>
[ApiController]
[Route("api/entry-forms")]
public sealed class EntryFormsController(IEntryFormService forms) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<IReadOnlyList<EntryFormDefDto>>> List([FromQuery] string? recordType, CancellationToken ct) =>
        Ok(await forms.ListAsync(recordType, ct));

    [HttpPost]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> Create([FromBody] SaveEntryFormRequest req, CancellationToken ct) =>
        Ok(await forms.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> Update(Guid id, [FromBody] SaveEntryFormRequest req, CancellationToken ct) =>
        Ok(await forms.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await forms.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/active")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> SetActive(Guid id, [FromBody] bool active, CancellationToken ct) =>
        Ok(await forms.SetActiveAsync(id, active, ct));

    [HttpPut("{id:guid}/roles")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> AssignRoles(Guid id, [FromBody] AssignRolesRequest req, CancellationToken ct) =>
        Ok(await forms.AssignRolesAsync(id, req, ct));

    /// <summary>The caller's form for a record type (A71): all-principal statically, the
    /// record type's View* checked dynamically inside the service — the caller can never
    /// name a form (OD-D7-2: server-side resolution is the anti-bypass, not just hygiene).</summary>
    [HttpGet("resolve")]
    [Action(ApiActions.ReadEntryForms)]
    public async Task<ActionResult<ResolvedFormDto>> Resolve([FromQuery] string recordType, CancellationToken ct) =>
        Ok(await forms.ResolveAsync(recordType, ct));

    // ---------- CF5-T2/T3: layout-object CRUD (A69, same guard as the composer) ----------

    [HttpPost("{id:guid}/subtabs")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> CreateSubtab(Guid id, [FromBody] SaveSubtabRequest req, CancellationToken ct) =>
        Ok(await forms.CreateSubtabAsync(id, req, ct));

    [HttpPut("{id:guid}/subtabs/{subtabId:guid}")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> UpdateSubtab(Guid id, Guid subtabId, [FromBody] SaveSubtabRequest req, CancellationToken ct) =>
        Ok(await forms.UpdateSubtabAsync(id, subtabId, req, ct));

    [HttpDelete("{id:guid}/subtabs/{subtabId:guid}")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> DeleteSubtab(Guid id, Guid subtabId, CancellationToken ct) =>
        Ok(await forms.DeleteSubtabAsync(id, subtabId, ct));

    [HttpPost("{id:guid}/groups")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> CreateGroup(Guid id, [FromBody] SaveGroupRequest req, CancellationToken ct) =>
        Ok(await forms.CreateGroupAsync(id, req, ct));

    [HttpPut("{id:guid}/groups/{groupId:guid}")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> UpdateGroup(Guid id, Guid groupId, [FromBody] SaveGroupRequest req, CancellationToken ct) =>
        Ok(await forms.UpdateGroupAsync(id, groupId, req, ct));

    [HttpDelete("{id:guid}/groups/{groupId:guid}")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> DeleteGroup(Guid id, Guid groupId, CancellationToken ct) =>
        Ok(await forms.DeleteGroupAsync(id, groupId, ct));

    // CF-FIX4-T3: the drag-drop placement move (ONE shared placement row, L1) + the
    // flat sublist column order (L4).
    [HttpPut("{id:guid}/fields/{fieldKey}/placement")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> MoveField(Guid id, string fieldKey, [FromBody] MoveFieldRequest req, CancellationToken ct) =>
        Ok(await forms.MoveFieldAsync(id, fieldKey, req, ct));

    [HttpDelete("{id:guid}/fields/{fieldKey}")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> RemoveField(Guid id, string fieldKey, CancellationToken ct) =>
        Ok(await forms.RemoveFieldAsync(id, fieldKey, ct));

    [HttpPut("{id:guid}/sublist")]
    [Action(ApiActions.ManageEntryForms)]
    public async Task<ActionResult<EntryFormDefDto>> SaveSublist(Guid id, [FromBody] SaveSublistRequest req, CancellationToken ct) =>
        Ok(await forms.SaveSublistAsync(id, req, ct));
}

/// <summary>Numbering schemes — Admin Setup (A70). Format-time config over the untouched
/// Slice G gap-free generator; history is never re-issued (OD-D7-6).</summary>
[ApiController]
[Route("api/numbering")]
public sealed class NumberingController(INumberingService numbering) : ControllerBase
{
    [HttpGet]
    [Action(ApiActions.ManageNumbering)]
    public async Task<ActionResult<IReadOnlyList<NumberingSchemeDto>>> List(CancellationToken ct) =>
        Ok(await numbering.ListAsync(ct));

    [HttpPut("{recordType}")]
    [Action(ApiActions.ManageNumbering)]
    public async Task<ActionResult<NumberingSchemeDto>> Update(string recordType, [FromBody] SaveNumberingSchemeRequest req, CancellationToken ct) =>
        Ok(await numbering.UpdateAsync(recordType, req, ct));
}
