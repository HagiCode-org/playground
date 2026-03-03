#!/bin/bash

# HagiCode mac ARM64 Download and Launch Script
# Downloads the latest mac ARM64 release from HagiCode releases and launches it

set -e  # Exit on error

# Configuration
REPO="HagiCode-org/releases"
DOWNLOAD_DIR="${HAGICODE_DIR:-/tmp/hagicode-mac-arm64}"
VERSION="${HAGICODE_VERSION:-latest}"
MAX_RETRIES=3
TIMEOUT_SECONDS=30

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1" >&2
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1" >&2
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

# Get latest version if not specified
get_latest_version() {
    if [ "$VERSION" != "latest" ]; then
        echo "$VERSION"
        return 0
    fi

    log_info "Fetching latest release version..."
    local version=$(curl -sSL --connect-timeout "$TIMEOUT_SECONDS" \
        "https://api.github.com/repos/${REPO}/releases/latest" | \
        grep '"tag_name":' | \
        sed -E 's/.*"([^"]*)".*/\1/')

    if [ -z "$version" ]; then
        log_error "Failed to fetch latest version"
        exit 1
    fi

    log_info "Latest version: $version"
    echo "$version"
}

# Download release with retry
download_release() {
    local version="$1"
    local filename="hagicode-${version}-osx-arm64-nort.zip"
    local url="https://github.com/${REPO}/releases/download/${version}/${filename}"

    log_info "Downloading from: $url"

    for attempt in $(seq 1 $MAX_RETRIES); do
        log_info "Download attempt $attempt/$MAX_RETRIES..."

        if curl -fsSL --connect-timeout "$TIMEOUT_SECONDS" \
            --retry 2 \
            --retry-delay 2 \
            -o "${DOWNLOAD_DIR}/${filename}" \
            "$url"; then
            log_info "Download successful"
            return 0
        fi

        log_warn "Download failed, retrying..."
        sleep 2
    done

    log_error "Failed to download after $MAX_RETRIES attempts"
    exit 1
}

# Verify and extract release
extract_release() {
    local filename="$1"

    log_info "Extracting release..."

    if ! unzip -q -o "${DOWNLOAD_DIR}/${filename}" -d "${DOWNLOAD_DIR}"; then
        log_error "Failed to extract archive"
        exit 1
    fi

    log_info "Extracted successfully to: $DOWNLOAD_DIR"
}

# Find and run startup script
run_application() {
    local startup_script="${DOWNLOAD_DIR}/start.sh"

    if [ ! -f "$startup_script" ]; then
        log_error "Startup script not found: $startup_script"
        log_error "Extracted contents:"
        ls -la "$DOWNLOAD_DIR"
        exit 1
    fi

    log_info "Making startup script executable..."
    chmod +x "$startup_script"

    log_info "Starting application..."
    log_info "Working directory: $DOWNLOAD_DIR"

    # Run the startup script
    cd "$DOWNLOAD_DIR"
    exec "$startup_script"
}

# Main execution
main() {
    log_info "HagiCode mac ARM64 Download and Launch Script"
    log_info "=============================================="

    # Create download directory
    mkdir -p "$DOWNLOAD_DIR"

    # Get version
    VERSION=$(get_latest_version)

    # Download release
    download_release "$VERSION"

    # Extract release
    local filename="hagicode-${VERSION}-osx-arm64-nort.zip"
    extract_release "$filename"

    # Run application
    run_application
}

# Run main function
main "$@"
