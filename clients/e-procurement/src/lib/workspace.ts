import { FshPermissions } from "@/lib/fsh-permissions";

const EVALUATOR_ROLES = new Set(["techevaluator", "commevaluator"]);
const BUYER_SHELL_ROLES = new Set(["admin", "buyer"]);

export function isEvaluatorRoleName(name: string | null | undefined): boolean {
  return EVALUATOR_ROLES.has((name ?? "").trim().toLowerCase());
}

export function isBuyerShellRoleName(name: string | null | undefined): boolean {
  return BUYER_SHELL_ROLES.has((name ?? "").trim().toLowerCase());
}

function looksLikeEvaluatorPermissions(granted: readonly string[]): boolean {
  const evalish =
    granted.includes(FshPermissions.evaluation.openTechnical) ||
    granted.includes(FshPermissions.evaluation.openCommercial) ||
    granted.includes(FshPermissions.evaluation.score);
  if (!evalish) return false;
  const buyerish =
    granted.includes(FshPermissions.rfqs.manageDraft) ||
    granted.includes(FshPermissions.requisitions.manage) ||
    granted.includes(FshPermissions.users.create) ||
    granted.includes(FshPermissions.award.submit);
  return !buyerish;
}

/** True when the signed-in (or Act-as) persona should get the evaluator shell, not buyer P2P. */
export function isEvaluatorWorkspace(opts: {
  isVendor: boolean;
  roles: readonly string[];
  permissions?: readonly string[];
  previewRoleName?: string | null;
}): boolean {
  if (opts.isVendor) return false;
  if (opts.previewRoleName) return isEvaluatorRoleName(opts.previewRoleName);
  if (opts.roles.some(isBuyerShellRoleName)) return false;
  if (opts.roles.some(isEvaluatorRoleName)) return true;
  return looksLikeEvaluatorPermissions(opts.permissions ?? []);
}

export function evaluatorKind(opts: {
  roles: readonly string[];
  permissions?: readonly string[];
  previewRoleName?: string | null;
}): { tech: boolean; commercial: boolean } {
  const names = opts.previewRoleName ? [opts.previewRoleName] : opts.roles;
  const lower = names.map((n) => n.trim().toLowerCase());
  let tech = lower.includes("techevaluator");
  let commercial = lower.includes("commevaluator");
  if (!tech && !commercial) {
    tech =
      (opts.permissions ?? []).includes(FshPermissions.evaluation.openTechnical) ||
      (opts.permissions ?? []).includes(FshPermissions.evaluation.score);
    commercial = (opts.permissions ?? []).includes(FshPermissions.evaluation.openCommercial);
  }
  return { tech, commercial };
}
