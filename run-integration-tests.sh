#!/bin/bash
# Run integration tests with required environment variable
export LOCAL_SETTINGS_PATH="$(pwd)/azure-function/local.settings.json"
echo "[INFO] LOCAL_SETTINGS_PATH set to $LOCAL_SETTINGS_PATH"
dotnet test tests/integration_tests/IngretationTests.csproj
