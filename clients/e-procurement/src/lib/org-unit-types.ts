/** Fixed OrgUnitType values — keep in sync with Platform OrgUnitType enum. */
export const ORG_UNIT_TYPES = [
  "Department",
  "Location",
  "CostCentre",
  "Category",
  "Project",
] as const;

export type OrgUnitTypeName = (typeof ORG_UNIT_TYPES)[number];

/** Classification fields on the PR form map 1:1 to these org-unit types. */
export const PR_CLASSIFICATION_ORG_TYPES = [
  "Department",
  "Location",
  "Category",
  "Project",
] as const satisfies readonly OrgUnitTypeName[];
