#!/bin/bash
# Wrapper script to refresh local.settings.json and build the solution
bash refresh-local-settings.sh
exec dotnet build serverless_stripe.sln "$@"
