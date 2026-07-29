# Local cluster

Runs the full stack (pgvector Postgres + Keycloak + backend API) on the local
single-node k3s cluster.

```sh
# build and publish the API image, then deploy
./build-image.sh
./deploy.sh
```

Both scripts are idempotent. `deploy.sh` prints the URLs and writes generated
credentials to `.generated-credentials` (gitignored).

| what | where |
| --- | --- |
| API | `http://<node-ip>:30081` — `/healthz`, `/swagger` |
| Keycloak | `http://<node-ip>:30880` — `/admin` |
| Postgres | `<node-ip>:30432` — db/user `tlmcplayer` |

Get a token for API calls:

```sh
set -a; . ./.generated-credentials; set +a
curl -s -X POST "$KEYCLOAK_REALM_URL/protocol/openid-connect/token" \
  -d "client_id=$KEYCLOAK_CLIENT_ID" -d "username=$TEST_USER" \
  -d "password=$TEST_USER_PASSWORD" -d grant_type=password
```

New Keycloak users must `POST /api/user` once before touching
`/api/playlists/*`; `Playlists.OwnerId` has a foreign key to `UserProfiles`, so
the personal-playlist bootstrap otherwise fails with a 500.

## Why this is separate from `K8s/`

These are not a patched copy of the production manifests — the differences are
specific to a local cluster and mostly should **not** be ported back:

- **NodePort instead of Ingress.** This cluster has no ingress controller
  (`kubectl get ingressclass` is empty), so `K8s/ingress.yaml` is not applied.
- **`local-path` / `ReadWriteOnce` volumes,** dynamically provisioned. The
  production PVCs request `ReadWriteMany` with no `storageClassName`, which
  `local-path` cannot bind.
- **Locally built image** from a `localhost:5000` registry, rather than
  `sqzd269/backend-api:latest` off Docker Hub. containerd allows plain HTTP for
  `localhost` registries, so this needs no `registries.yaml` and no root.
- **Self-contained Keycloak realm** imported from
  `realm-MusicPlayer.template.json`, instead of depending on
  `sso.marisad.me` (which also means local dev keeps working when prod SSO is
  down).
- **Generated credentials,** created once into Secrets and never committed.
- **`ASPNETCORE_ENVIRONMENT=Staging`.** Not `Development`, which would expose
  the unauthenticated `InternalController` write surface on the LAN; not
  `Production`, which force-rewrites `Request.Scheme` to `https` and breaks
  link generation on a plain-HTTP cluster. Flip to `Development` only if you
  need the `api/internal` endpoints for an ETL push.

A few differences *are* fixes for real bugs in `K8s/` and should be ported:

- `keycloak-server-clusterip` targets port **80**, but the Keycloak container
  listens on **8080** — as committed, the service matches nothing and
  `ingress.yaml` asks for a port the service does not expose.
- `keycloak-server-nodeport` has no `namespace:`, so it lands in `default` and
  its selector never matches.
- `pgvector/pgvector:pg15` is a floating tag; `halfvec` needs pgvector >= 0.7.
  Pinned here to `0.8.6-pg15`.
- No probes, no resource requests/limits on any workload.

## Publishing Keycloak through the Cloudflare tunnel

```sh
KC_PUBLIC_URL=https://sso.marisad.me ./deploy.sh
```

That switches three things together, because they have to agree: Keycloak
advertises the public URL as its token issuer, `KC_PROXY` becomes `edge` so it
emits `https://` links despite serving plain HTTP internally, and the backend
sets `Keycloak__RequireHttpsMetadata` back to `true` (the local HTTP relaxation
is no longer needed once TLS terminates at Cloudflare's edge).

The API keeps reaching Keycloak at the public hostname rather than taking an
in-cluster shortcut. That looks wasteful, but the token's `iss` claim is the
public URL and `ValidIssuers` compares it byte-for-byte, so fetching discovery
from a service DNS name would report an issuer the API then rejects.

Omit `KC_PUBLIC_URL` to go back to the direct NodePort; nothing else needs
changing. The tunnel itself is configured on the host, not here — see the
`services.cloudflared` block in the NixOS config (`/etc/nixos/configuration.nix`),
which includes the one-time `cloudflared tunnel` steps.

Note that publishing Keycloak also publishes self-registration: the imported
realm sets `registrationAllowed: true`, so anyone reaching `sso.marisad.me`
could create an account. Turn that off in the realm (or in
`realm-MusicPlayer.template.json`) before leaving the tunnel up.

## Keycloak port is load-bearing

`KC_NODEPORT` (default 30880 — 30080 is taken by headlamp) appears in three
places that must agree exactly: the NodePort, Keycloak's `KC_HOSTNAME_URL`
(which becomes the `iss` claim), and the backend's `Keycloak__RealmUrl` (which
`ValidIssuers` compares byte-for-byte). `deploy.sh` substitutes all three from
that one variable. It is deliberately the node IP rather than a service DNS
name, so the same issuer resolves both inside the cluster and from the LAN.

## Backend code changes this required

- `Keycloak:RequireHttpsMetadata` (new, defaults to **true** so production is
  unchanged). OIDC discovery hard-set `RequireHttps = true`, which no
  plain-HTTP local Keycloak can satisfy. Mirrors
  `JwtBearerOptions.RequireHttpsMetadata`.
- `/healthz` via `AddHealthChecks()`. The app had no health endpoint, so
  probes had nothing valid to hit.
- `TlmcPlayerBackend/Dockerfile` only copied `TlmcPlayerBackend.csproj` before
  `dotnet restore`, so restore could not resolve the `KeycloakAuthProvider`
  project reference. The image could not build as committed.

## Teardown

```sh
kubectl delete ns tlmc-player     # destroys the databases too
```
