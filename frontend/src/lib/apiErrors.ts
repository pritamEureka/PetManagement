/**
 * Stable backend error codes (ApiError.code from ExceptionMiddleware / AppException)
 * mapped to catalog keys `err.<code>`. Keep in sync with the backend:
 *   grep -rn 'AppException("' backend/src
 */
export const API_ERROR_CODES = [
  "not_found",
  "forbidden",
  "conflict",
  "validation_failed",
  "bad_request",
  "unauthorized",
  "internal_error",
  "rate_limited",
  "step_up_required",
  "two_factor_required",
  "consent_required",
  "account_suspended",
  "registration_pending",
  "registration_rejected",
  "questionnaire_required",
  "spam_blocked",
  "invalid_event",
  "invalid_occurrences",
  "invalid_range",
  "invalid_reaction",
  "invalid_slot",
  "invalid_type",
  "invalid_waitlist",
  "not_online",
  "note_too_long",
  "reschedule_window_closed",
  "idempotency_in_flight",
  "idempotency_key_invalid",
  "idempotency_key_reused",
] as const;

export type ApiErrorCode = (typeof API_ERROR_CODES)[number];

export const errKey = (code: string) => `err.${code}`;

interface ApiErrorBody {
  error?: { code?: string; message?: string; details?: unknown };
}

type Translate = (key: string, vars?: Record<string, string | number>) => string;

/** Field-level validation messages from `error.details` ({ field: string[] } | string[] | ...). */
export function fieldErrors(details: unknown): Record<string, string[]> {
  const out: Record<string, string[]> = {};
  if (!details || typeof details !== "object") return out;
  if (Array.isArray(details)) {
    const msgs = details.filter((d): d is string => typeof d === "string");
    if (msgs.length) out[""] = msgs;
    return out;
  }
  for (const [k, v] of Object.entries(details as Record<string, unknown>)) {
    if (Array.isArray(v)) out[k] = v.filter((x): x is string => typeof x === "string");
    else if (typeof v === "string") out[k] = [v];
  }
  return out;
}

/**
 * Human message for an API error body: translated by stable code when the
 * catalog knows it, otherwise the server message, otherwise a generic fallback.
 * For validation_failed, the first field message is appended when present.
 */
export function messageForApiError(
  body: ApiErrorBody | undefined,
  t: Translate,
  has: (key: string) => boolean,
  fallback?: string,
  /** English UI: keep the server's specific message for generic codes (conflict, not_found, ...). */
  preferServerForGeneric = false,
): string {
  const code = body?.error?.code;
  const serverMsg = body?.error?.message;
  let msg: string | undefined;
  const generic = ["conflict", "bad_request", "validation_failed", "not_found", "forbidden", "internal_error"].includes(code ?? "");
  if (code && has(errKey(code)) && !(preferServerForGeneric && generic && serverMsg)) msg = t(errKey(code));
  msg ??= serverMsg ?? fallback ?? t("common.error");
  if (code === "validation_failed") {
    const first = Object.entries(fieldErrors(body?.error?.details)).find(([, v]) => v.length > 0);
    if (first) msg += ` ${first[0] ? first[0] + ": " : ""}${first[1][0]}`;
  }
  return msg;
}
