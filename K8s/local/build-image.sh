#!/usr/bin/env bash
# Build the backend image and publish it where the local cluster can pull it.
#
# k3s's containerd socket is root-owned, so `ctr images import` needs sudo.
# A registry on localhost avoids that entirely: containerd allows plain HTTP
# for localhost registries, so no registries.yaml and no root is required.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HERE/../.." && pwd)"

REGISTRY_PORT="${REGISTRY_PORT:-5000}"
REGISTRY_NAME="${REGISTRY_NAME:-tlmc-registry}"
IMAGE="${IMAGE:-localhost:$REGISTRY_PORT/tlmc-backend-api:local}"

if [[ -z "$(docker ps -q -f "name=^${REGISTRY_NAME}$")" ]]; then
  if [[ -n "$(docker ps -aq -f "name=^${REGISTRY_NAME}$")" ]]; then
    echo "==> starting existing registry container"
    docker start "$REGISTRY_NAME" >/dev/null
  else
    echo "==> creating registry on 127.0.0.1:$REGISTRY_PORT"
    docker run -d --restart=unless-stopped \
      -p "127.0.0.1:$REGISTRY_PORT:5000" \
      --name "$REGISTRY_NAME" registry:2 >/dev/null
  fi
fi

echo "==> building $IMAGE"
docker build -t "$IMAGE" -f "$REPO_ROOT/TlmcPlayerBackend/Dockerfile" "$REPO_ROOT"

echo "==> pushing"
docker push "$IMAGE"

echo "==> done: $IMAGE"
echo "    deploy.sh forces a rollout restart, so a re-push of the same tag is picked up."
