#!/usr/bin/env bash
# Stand up the whole stack on a local single-node cluster (k3s).
#
# Differs from K8s/ (the production manifests) in ways that are specific to a
# local cluster and are NOT bugs to port back: NodePort instead of Ingress,
# local-path RWO volumes, a locally built image from a localhost registry, and
# a self-contained Keycloak realm. Credentials are generated here and never
# committed.
#
# Idempotent: safe to re-run. Secrets are created once and then reused, because
# Postgres only honours POSTGRES_PASSWORD at initdb time -- rotating it later
# would lock the API out of an already-initialised database.
set -euo pipefail

NS=tlmc-player
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CREDS="$HERE/.generated-credentials"
RENDERED_REALM="$HERE/realm-MusicPlayer.rendered.json"

IMAGE="${IMAGE:-localhost:5000/tlmc-backend-api:local}"
REGISTRY_PORT="${REGISTRY_PORT:-5000}"

# Keycloak's NodePort is load-bearing beyond routing: it appears in the token
# issuer (KC_HOSTNAME_URL) and in the backend's Keycloak__RealmUrl, which the
# backend compares byte-for-byte. One knob keeps all three in sync.
# 30080 is taken by headlamp on this cluster.
KC_NODEPORT="${KC_NODEPORT:-30880}"
API_NODEPORT="${API_NODEPORT:-30081}"
PG_NODEPORT="${PG_NODEPORT:-30432}"
MEILI_NODEPORT="${MEILI_NODEPORT:-30700}"

# Set KC_PUBLIC_URL to publish Keycloak through the Cloudflare tunnel, e.g.
#   KC_PUBLIC_URL=https://sso.marisad.me ./deploy.sh
# The issuer must then be the public URL on both sides, so the API validates
# tokens against it and fetches discovery through the tunnel too -- an internal
# shortcut would report the public issuer and fail the byte-for-byte compare.
KC_PUBLIC_URL="${KC_PUBLIC_URL:-}"

NODE_IP="${NODE_IP:-$(kubectl get node -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}')}"
if [[ -z "$NODE_IP" ]]; then
  echo "could not determine node InternalIP; set NODE_IP=..." >&2
  exit 1
fi
echo "==> node ip: $NODE_IP"

if [[ -n "$KC_PUBLIC_URL" ]]; then
  KC_ISSUER_BASE="${KC_PUBLIC_URL%/}"
  KC_PROXY_MODE=edge
  KC_REQUIRE_HTTPS=true
  echo "==> keycloak issuer: $KC_ISSUER_BASE (public, via tunnel)"
else
  KC_ISSUER_BASE="http://$NODE_IP:$KC_NODEPORT"
  KC_PROXY_MODE=none
  KC_REQUIRE_HTTPS=false
  echo "==> keycloak issuer: $KC_ISSUER_BASE (direct nodeport)"
fi

# No openssl dependency: the input to base64 is a fixed 48 bytes, so every
# stage of the pipe terminates on its own rather than on SIGPIPE (which
# `set -o pipefail` would otherwise turn into a failure).
gen_pw() { head -c 48 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 28; }

echo "==> namespace"
kubectl apply -f "$HERE/../namespace.yaml" >/dev/null

if ! kubectl -n "$NS" get secret backend-pgsql-credentials >/dev/null 2>&1; then
  echo "==> generating credentials (first run)"
  BACKEND_PG_PW="$(gen_pw)"
  KC_PG_PW="$(gen_pw)"
  KC_ADMIN_PW="$(gen_pw)"
  TEST_USER_PW="$(gen_pw)"

  kubectl -n "$NS" create secret generic backend-pgsql-credentials \
    --from-literal=POSTGRES_PASSWORD="$BACKEND_PG_PW" >/dev/null
  kubectl -n "$NS" create secret generic keycloak-pgsql-credentials \
    --from-literal=POSTGRES_PASSWORD="$KC_PG_PW" >/dev/null
  kubectl -n "$NS" create secret generic keycloak-admin-credentials \
    --from-literal=KEYCLOAK_ADMIN=admin \
    --from-literal=KEYCLOAK_ADMIN_PASSWORD="$KC_ADMIN_PW" \
    --from-literal=TEST_USER_PASSWORD="$TEST_USER_PW" >/dev/null
  INTERNAL_API_KEY="$(gen_pw)"
  kubectl -n "$NS" create secret generic backend-api-config \
    --from-literal=ConnectionStrings__PostgreSql="Host=backend-pgsql-clusterip.$NS.svc.cluster.local;Port=5432;Database=tlmcplayer_v6;Username=tlmcplayer;Password=$BACKEND_PG_PW" \
    --from-literal=Internal__ApiKey="$INTERNAL_API_KEY" >/dev/null
else
  echo "==> reusing existing credentials"
fi

# Read back from the cluster rather than from the branch above, so the file is
# regenerated correctly on re-runs too -- the URLs in it change when
# KC_PUBLIC_URL does, even though the passwords never do.
secret_val() {
  kubectl -n "$NS" get secret "$1" -o jsonpath="{.data.$2}" | base64 -d
}

# Backfill keys added after a secret was first created. Without this, a cluster
# deployed by an older revision of this script keeps a secret missing the new key,
# and the pod fails to start on a missing secretKeyRef rather than saying why.
ensure_secret_key() {
  local secret="$1" key="$2" value="$3"
  if [[ -z "$(kubectl -n "$NS" get secret "$secret" -o jsonpath="{.data.$key}")" ]]; then
    echo "    adding $key to $secret"
    kubectl -n "$NS" patch secret "$secret" \
      --type merge -p "{\"stringData\":{\"$key\":\"$value\"}}" >/dev/null
  fi
}
ensure_secret_key backend-api-config Internal__ApiKey "$(gen_pw)"

# Its own secret (not a key on backend-api-config): the Meilisearch container
# consumes it too, and unlike the Postgres password it is safe to exist late --
# a cluster deployed by an older script revision just gains it on re-run.
if ! kubectl -n "$NS" get secret meilisearch-credentials >/dev/null 2>&1; then
  echo "==> generating meilisearch master key"
  kubectl -n "$NS" create secret generic meilisearch-credentials \
    --from-literal=MEILI_MASTER_KEY="$(gen_pw)" >/dev/null
fi

TEST_USER_PW="$(secret_val keycloak-admin-credentials TEST_USER_PASSWORD)"

( umask 077
  cat > "$CREDS" <<EOF
# Generated by K8s/local/deploy.sh -- gitignored, keep it that way.
BACKEND_POSTGRES_USER=tlmcplayer
BACKEND_POSTGRES_DB=tlmcplayer_v6
BACKEND_POSTGRES_PASSWORD=$(secret_val backend-pgsql-credentials POSTGRES_PASSWORD)
BACKEND_POSTGRES_HOSTPORT=$NODE_IP:$PG_NODEPORT
KEYCLOAK_PG_PASSWORD=$(secret_val keycloak-pgsql-credentials POSTGRES_PASSWORD)
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=$(secret_val keycloak-admin-credentials KEYCLOAK_ADMIN_PASSWORD)
KEYCLOAK_URL=$KC_ISSUER_BASE
KEYCLOAK_REALM_URL=$KC_ISSUER_BASE/realms/MusicPlayer
KEYCLOAK_CLIENT_ID=tlmc-player-web
TEST_USER=testuser
TEST_USER_PASSWORD=$TEST_USER_PW
# Send as the X-Internal-Api-Key header to use the api/internal write surface.
INTERNAL_API_KEY=$(secret_val backend-api-config Internal__ApiKey)
API_URL=http://$NODE_IP:$API_NODEPORT
# Bearer token for direct Meilisearch access (the CJK probe, index inspection).
MEILI_MASTER_KEY=$(secret_val meilisearch-credentials MEILI_MASTER_KEY)
MEILI_URL=http://$NODE_IP:$MEILI_NODEPORT
EOF
)
echo "    credentials: $CREDS"

echo "==> keycloak realm import configmap"
( umask 077
  sed "s|__TEST_USER_PASSWORD__|$TEST_USER_PW|g" \
    "$HERE/realm-MusicPlayer.template.json" > "$RENDERED_REALM"
)
kubectl -n "$NS" create configmap keycloak-realm-import \
  --from-file=realm-MusicPlayer.json="$RENDERED_REALM" \
  --dry-run=client -o yaml | kubectl apply -f - >/dev/null

apply() {
  sed -e "s|__NODE_IP__|$NODE_IP|g" \
      -e "s|__KC_NODEPORT__|$KC_NODEPORT|g" \
      -e "s|__KC_ISSUER_BASE__|$KC_ISSUER_BASE|g" \
      -e "s|__KC_PROXY_MODE__|$KC_PROXY_MODE|g" \
      -e "s|__KC_REQUIRE_HTTPS__|$KC_REQUIRE_HTTPS|g" \
      "$1" | kubectl apply -f - >/dev/null
}

echo "==> databases"
apply "$HERE/10-backend-pgsql.yaml"
apply "$HERE/15-meilisearch.yaml"
apply "$HERE/20-keycloak-pgsql.yaml"
kubectl -n "$NS" rollout status deploy/backend-pgsql --timeout=300s
kubectl -n "$NS" rollout status deploy/meilisearch --timeout=300s
kubectl -n "$NS" rollout status deploy/keycloak-pgsql --timeout=300s

echo "==> keycloak"
apply "$HERE/30-keycloak.yaml"
kubectl -n "$NS" rollout status deploy/keycloak --timeout=600s

echo "==> backend api"
apply "$HERE/40-backend-api.yaml"
# Restart so a freshly pushed :local image is picked up even when the tag is unchanged.
kubectl -n "$NS" rollout restart deploy/backend-api >/dev/null
kubectl -n "$NS" rollout status deploy/backend-api --timeout=600s

echo
echo "==> up"
kubectl -n "$NS" get pods -o wide
cat <<EOF

  API          http://$NODE_IP:$API_NODEPORT   (health: /healthz, docs: /swagger)
  Keycloak     $KC_ISSUER_BASE   (admin console: /admin)
  Postgres     $NODE_IP:$PG_NODEPORT        (db/user: tlmcplayer)
  Meilisearch  http://$NODE_IP:$MEILI_NODEPORT   (auth: MEILI_MASTER_KEY as bearer)

  Credentials: $CREDS
EOF

if [[ -n "$KC_PUBLIC_URL" ]]; then
  cat <<EOF
  Keycloak is published through the Cloudflare tunnel. The API resolves that
  hostname at startup to fetch signing keys, so it cannot boot while the tunnel
  is down -- check cloudflared before debugging the API.
  Direct NodePort (bypasses the tunnel): http://$NODE_IP:$KC_NODEPORT
EOF
fi
