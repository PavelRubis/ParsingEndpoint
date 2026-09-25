FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /workspace
RUN dotnet tool install yuniql.cli --version 1.3.15 --tool-path /tools
ENV DOTNET_ROLL_FORWARD=Major
EXPOSE 8090
