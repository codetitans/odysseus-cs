#!/bin/sh

set -e -o pipefail

#######################################
# global variables

PACKAGE_REPOSITORY_URL=https://git.codetitans.pl/api/packages/CodeTitans/nuget/index.json
PROJECT_NAME=CodeTitans.OdysseusClient
TOOL_NAME=CodeTitans.Odysseus

#######################################

print_usage() {
    cat <<EOF

Script to build or publish CodeTitans Odysseus Client Project.

Usage: sh $0 <options> <command> <args>

Commands:
 tool            - build the NuGet tool package

[CodeTitans 2025]
EOF
}

#######################################


build_and_tag_package() {
    local version=$1

    if [ -z "$version" ]; then
        echo Missing version of the tool package: \'$TOOL_NAME\'...
        exit 1
    fi

    local tag_name=v$version

    echo "Building the tool..."
    dotnet pack "$PROJECT_NAME" --configuration Release -p:PackageVersion=$version -p:Version=$version --nologo

    echo "Applying tag $tag_name..."
    git tag "${tag_name}" -m "Release v${version} of the project." || true
}

publish() {
    local version=$1
    local token_file=${2:-gitea.token}
    local key=$3

    if [ -z "$version" ]; then
        echo Missing version of the tool package to publish: \'$TOOL_NAME\'...
        exit 1
    fi

    if [ -z "$key" -a -r "$token_file" ]; then
        key=`cat "${token_file}"`
    fi

    if [ -z "$key" ]; then
        echo Missing the Personal Token to upload package\! Pass it as last argument or via \"${token_file}\" file.
        exit 1
    fi

    echo "Publishing..."
    dotnet nuget push "../publish-tool/${TOOL_NAME}.${version}.nupkg" \
        --api-key "$key" --source "${PACKAGE_REPOSITORY_URL}" --skip-duplicate --no-symbols
}

install_package() {
    local version=$1

    if [ -z "$version" ]; then
        dotnet tool install --global --no-cache --add-source ./publish-tool --ignore-failed-sources "$TOOL_NAME"
    else
        dotnet tool install --global --no-cache --add-source ./publish-tool --ignore-failed-sources "$TOOL_NAME" --version "$version"
    fi
}

#######################################

if [ "$#" -eq 0 ]; then
    print_usage
    exit 1
fi

# process options:
for arg in "$@"
do
    break
done


case "$1" in
    "gitea" | "gitea-publish")
        (
            cd src/
            build_and_tag_package "$2" "$3"
            publish "$2" "../gitea.token" "$4"
        )
    ;;

    "gitea-push")
        (
            cd src/
            publish "$2" "../gitea.token" "$3"
        )
    ;;

    "github-push")
        (
            cd src/
            publish "$2" "../github.token" "$3"
        )
    ;;

    "nuget" | "build" | "tool" | "pack")
        (
            cd src/
            build_and_tag_package "$2" "$3"
        )
    ;;

    "install" | "deploy")
        echo "HINT: in case of errors, make sure all custom Package Repositories added by Rider are disabled."
        install_package "$2"
    ;;

    "uninstall" | "remove")
        dotnet tool uninstall --global "$TOOL_NAME"
    ;;

    "update" | "upgrade")
        dotnet tool update --no-cache --ignore-failed-sources --global --add-source ./publish-tool "$TOOL_NAME"
    ;;

    *)
        echo "Unrecognized command: '$1'"
        exit 250
    ;;
esac

echo "~~~~~~~~~~"
echo Completed\!
