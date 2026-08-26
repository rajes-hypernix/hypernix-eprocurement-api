/** In-memory flag so apiFetch can block writes during role preview (not persisted). */
let rolePreviewActive = false;

export function setRolePreviewActive(active: boolean): void {
  rolePreviewActive = active;
}

export function isRolePreviewActive(): boolean {
  return rolePreviewActive;
}
