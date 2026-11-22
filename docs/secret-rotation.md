# Secret rotation and external secret manager

## Source of truth
`deploy/k8s/base/secret.yaml` is an empty template. In real clusters use External Secrets Operator
(`deploy/k8s/base/external-secret.example.yaml`): remove `secret.yaml` from the kustomization and let the
`ExternalSecret` own `Secret/pawzaroo-secrets`. Keys: POSTGRES_PASSWORD, JWT_SIGNING_KEY, STORAGE_ACCESS_KEY,
STORAGE_SECRET_KEY, REDIS_PASSWORD, SUPERADMIN_PASSWORD (optional).

Secrets are consumed as environment variables, so a rotated value only takes effect after pods restart:
`kubectl -n <ns> rollout restart deploy/api deploy/worker`. (Optionally run Stakater Reloader to automate this.)

## JWT signing key - overlapping rotation
Implemented in `Infrastructure/Identity/JwtKeyRing.cs`:
- Tokens are ALWAYS signed with `Jwt:SigningKey` (current) and carry a `kid` header = first 16 hex chars of SHA-256 of the key.
- `Jwt:PreviousSigningKeys` (array `Jwt__PreviousSigningKeys__0`... or one comma/semicolon separated string) are accepted for
  VALIDATION only. A token whose `kid` matches no configured key is rejected; legacy tokens without a `kid` are tried against
  every key. `StartupSecretValidator` requires each previous key to be >= 32 chars (and, outside Development, free of
  placeholder values) and refuses a previous key equal to the current one.
- Requires the one-line wiring in `Api/Program.cs` (`IssuerSigningKeyResolver = JwtKeyRing.FromConfiguration(builder.Configuration).Resolver`).
  Until that line is in place, Program.cs still validates against the single current key and rotation is NOT overlapping.

Zero-downtime procedure (let N = access-token lifetime, `Jwt:AccessTokenMinutes`):
1. Generate a new key (`openssl rand -base64 48`). Set `Jwt:PreviousSigningKeys` = old key and `Jwt:SigningKey` = new key
   (secret manager; add `JWT_PREVIOUS_SIGNING_KEYS` to the secret and an env mapping to `Jwt__PreviousSigningKeys` - the base
   manifests do not map it yet).
2. Roll `api` and `worker`. Old pods still accept new-key tokens only if they already ship this code, so do the FIRST rollout
   that introduces `JwtKeyRing` with the current key unchanged, then rotate in a second rollout.
3. After > N (plus refresh-driven re-issuance, which happens on the next refresh) remove the old key from `PreviousSigningKeys`
   and roll again. Revoke the old value in the secret manager.

Known coupling that rotation does NOT cover:
- `TwoFactorService` derives its TOTP-secret encryption key from `Jwt:SigningKey` (+ ":2fa"). Rotating the JWT key makes existing
  2FA secrets undecryptable (users must re-enrol) until that service gets its own key. Do not rotate in production before fixing it
  or setting up a re-encryption step.
- `Storage:UrlSigningKey` (media link HMAC) falls back to the JWT key, so rotating invalidates signed media links unless it is set
  separately.
- Refresh tokens are opaque DB records and are unaffected.

## Other secrets
- Postgres: set the new role password in the DB first (`ALTER ROLE ... PASSWORD`), update the secret, restart pods.
  For zero downtime use two roles and alternate (dual-user rotation).
- MinIO/S3 keys: create a second access key, switch the secret, restart, delete the old key.
- Redis password: update server and secret, restart (cache only, brief degradation).
- SuperAdmin bootstrap password matters only on first boot; change the account password in-app afterwards.

## Cadence
JWT and DB credentials every 90 days or on staff change; storage keys every 180 days; immediately on suspected leak.
