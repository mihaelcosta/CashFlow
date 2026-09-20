FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props CashFlow.slnx ./
COPY src/CashFlow.Domain/CashFlow.Domain.csproj src/CashFlow.Domain/
COPY src/CashFlow.Application/CashFlow.Application.csproj src/CashFlow.Application/
COPY src/CashFlow.Infrastructure/CashFlow.Infrastructure.csproj src/CashFlow.Infrastructure/
COPY src/CashFlow.Api/CashFlow.Api.csproj src/CashFlow.Api/
RUN dotnet restore src/CashFlow.Api/CashFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/CashFlow.Api/CashFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir /data && chown app:app /data
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    Database__ConnectionString="Data Source=/data/cashflow.db"

VOLUME /data
EXPOSE 8080

COPY --from=build /app .
ENTRYPOINT ["dotnet", "CashFlow.Api.dll"]
