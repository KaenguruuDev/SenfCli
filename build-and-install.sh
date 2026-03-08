#!/usr/bin/env bash

INSTALL_PATH="/usr/local/bin"
ADD_TO_PATH=false
SHOW_HELP=false

usage() {
    echo "SenfCli Build and Install Script"
    echo ""
    echo "Usage: ./build-and-install.sh [options]"
    echo ""
    echo "Options:"
    echo "  --install-path <path>   Installation directory (default: \$HOME/.senf/bin)"
    echo "  --add-to-path           Automatically add installation path to ~/.profile"
    echo "  --help                  Show this help message"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --install-path)
            INSTALL_PATH="$2"
            shift 2
            ;;
        --add-to-path)
            ADD_TO_PATH=true
            shift
            ;;
        --help)
            SHOW_HELP=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

if $SHOW_HELP; then
    usage
    exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/SenfCli.csproj"
PUBLISH_PATH="$SCRIPT_DIR/bin/publish"

if [[ ! -f "$PROJECT_FILE" ]]; then
    echo "ERROR: Could not find project file at $PROJECT_FILE"
    exit 1
fi

echo "Building SenfCli..."

dotnet publish "$PROJECT_FILE" \
    -c Release \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o "$PUBLISH_PATH"

echo "Build successful"

mkdir -p "$INSTALL_PATH"

echo "Copying files to $INSTALL_PATH"
cp -r "$PUBLISH_PATH"/. "$INSTALL_PATH/"

SHIM_PATH="$INSTALL_PATH/senf"

if [ ! -w "$INSTALL_PATH" ]; then
    echo "ERROR: Cannot write to $INSTALL_PATH. Check permissions."
    exit 1
fi

/bin/cat > "$SHIM_PATH" << "EOF"
#!/usr/bin/env bash
exec "$INSTALL_PATH/SenfCli" "$@"
EOF

if [ ! -f "$SHIM_PATH" ]; then
    echo "ERROR: Failed to create shim file at $SHIM_PATH."
    exit 1
fi

chmod +x "$SHIM_PATH"
chmod +x "$INSTALL_PATH/SenfCli"

if $ADD_TO_PATH; then
    PROFILE_FILE="$HOME/.profile"
    EXPORT_LINE="export PATH=\"\$PATH:$INSTALL_PATH\""

    if ! grep -qF "$INSTALL_PATH" "$PROFILE_FILE" 2>/dev/null; then
        echo "" >> "$PROFILE_FILE"
        echo "# Added by SenfCli installer" >> "$PROFILE_FILE"
        echo "$EXPORT_LINE" >> "$PROFILE_FILE"
        echo "PATH updated in $PROFILE_FILE (restart your shell or run: source $PROFILE_FILE)"
    else
        echo "PATH already contains $INSTALL_PATH — skipping"
    fi
fi

echo "Installation complete"
echo "Installed binary: $INSTALL_PATH/SenfCli"
echo "Command shim:     $SHIM_PATH"
