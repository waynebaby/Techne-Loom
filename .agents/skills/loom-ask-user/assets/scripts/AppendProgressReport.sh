#!/usr/bin/env bash
set -euo pipefail

fail() {
    printf 'AppendProgressReport.sh: %s\n' "$1" >&2
    exit 1
}

target_file=''
entry_file=''

while (($# > 0)); do
    case "$1" in
        --target-file)
            (($# >= 2)) || fail 'Missing value for --target-file.'
            [[ -z "$target_file" ]] || fail 'Duplicate --target-file option.'
            target_file=$2
            shift 2
            ;;
        --entry-file)
            (($# >= 2)) || fail 'Missing value for --entry-file.'
            [[ -z "$entry_file" ]] || fail 'Duplicate --entry-file option.'
            entry_file=$2
            shift 2
            ;;
        *)
            fail "Unknown argument: $1"
            ;;
    esac
done

[[ -n "$target_file" ]] || fail 'Required option --target-file is missing.'
[[ -n "$entry_file" ]] || fail 'Required option --entry-file is missing.'
[[ "$target_file" = /* ]] || fail 'Target path must be absolute.'
[[ "$entry_file" = /* ]] || fail 'Entry path must be absolute.'
[[ "${target_file##*/}" == 'progress-report.md' ]] || fail 'Target must be named progress-report.md.'
[[ -f "$entry_file" && ! -L "$entry_file" ]] || fail 'Entry file must be a regular non-symlink file.'
[[ ! -L "$target_file" ]] || fail 'Target cannot be a symbolic link.'
[[ ! -e "$target_file" || -f "$target_file" ]] || fail 'Target must be a regular file.'
[[ -d "${target_file%/*}" ]] || fail 'Target directory does not exist.'

entry_directory=$(cd -P "${entry_file%/*}" && pwd -P)
target_directory=$(cd -P "${target_file%/*}" && pwd -P)
normalized_entry="${entry_directory}/${entry_file##*/}"
normalized_target="${target_directory}/${target_file##*/}"
[[ "$normalized_entry" != "$normalized_target" ]] || fail 'The entry file and target file must be different.'
[[ -s "$entry_file" ]] || fail 'Entry file is empty.'
iconv -f UTF-8 -t UTF-8 "$entry_file" >/dev/null 2>&1 || fail 'Entry file must be valid UTF-8.'
last_byte=$(tail -c 1 "$entry_file" | od -An -t x1 | tr -d '[:space:]')
[[ "$last_byte" == '0a' ]] || fail 'Entry file must end with a line feed.'

lock_directory="${normalized_target}.append-lock"
if ! mkdir "$lock_directory" 2>/dev/null; then
    fail 'Another append is active for this target or a stale append lock exists.'
fi
cleanup_lock() {
    rmdir "$lock_directory" 2>/dev/null || true
}
trap cleanup_lock EXIT

{
    printf '\n'
    cat "$entry_file"
} >> "$target_file"

url_path=${normalized_target//%/%25}
url_path=${url_path// /%20}
url_path=${url_path//#/%23}
url_path=${url_path//\?/%3F}
case "$url_path" in
    /*) ;;
    *) url_path="/$url_path" ;;
esac
printf 'file://%s\n' "$url_path"
