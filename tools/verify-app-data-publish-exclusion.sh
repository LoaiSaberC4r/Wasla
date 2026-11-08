#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd -- "$repo_root"
media_root="$repo_root/src/Wasla/Wasla.Api/App_Data/Wasla/Media"
publish_parent="$(cd -- "${TMPDIR:-/tmp}" && pwd -P)"
sentinel=''
publish_dir=''

cleanup() {
  local result=$?
  trap - EXIT
  if [[ -n "$sentinel" ]]; then
    if [[ "$sentinel" == "$media_root"/.wasla-publish-sentinel.*.json ]]; then
      rm -f -- "$sentinel" || result=1
    else
      echo 'Refusing to clean an unexpected sentinel path.' >&2
      result=1
    fi
  fi
  if [[ -n "$publish_dir" && -d "$publish_dir" ]]; then
    # Only recursively remove the exact canonical directory owned by this invocation.
    if [[ "$(dirname -- "$publish_dir")" == "$publish_parent" &&
          "$(basename -- "$publish_dir")" == wasla-app-data-publish.* &&
          "$(cd -- "$publish_dir" && pwd -P)" == "$publish_dir" ]]; then
      rm -rf -- "$publish_dir" || result=1
    else
      echo 'Refusing to clean an unexpected publish directory.' >&2
      result=1
    fi
  fi
  exit "$result"
}
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

mkdir -p -- "$media_root"
sentinel="$(mktemp "$media_root/.wasla-publish-sentinel.XXXXXXXX.json")"
printf '%s\n' '{"synthetic":"App_Data publish exclusion verification"}' > "$sentinel"
publish_dir="$(mktemp -d "$publish_parent/wasla-app-data-publish.XXXXXXXX")"

dotnet publish src/Wasla/Wasla.Api/Wasla.Api.csproj \
  --configuration Release --no-restore --output "$publish_dir"

published_app_data="$(find "$publish_dir" -iname App_Data -print -quit)"
published_sentinel="$(find "$publish_dir" -name "$(basename -- "$sentinel")" -print -quit)"
if [[ -n "$published_app_data" || -n "$published_sentinel" ]]; then
  echo 'FAIL: private App_Data content was included in Release publish output.' >&2
  exit 1
fi
echo 'PASS: Release publish excludes App_Data and the private-storage sentinel.'
