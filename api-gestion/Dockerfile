# ================================
# Étape 1 : Build & Publish
# ================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauration des dépendances (mise en cache des layers Docker)
# -r linux-x64 : nécessaire pour que les bibliothèques natives (SkiaSharp pour
# la génération des codes-barres/PDF) soient bien restaurées puis publiées.
COPY api-gestion.csproj ./
RUN dotnet restore "api-gestion.csproj" -r linux-x64

# Copie du reste du code source et publication en mode Release
COPY . .
RUN dotnet publish "api-gestion.csproj" -c Release -o /app/publish -r linux-x64 --self-contained false /p:UseAppHost=false

# ================================
# Étape 2 : Image d'exécution
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Dépendances natives requises par SkiaSharp (génération des codes-barres et des PDF)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libfontconfig1 \
        libfreetype6 \
        libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableWriteXorExecute=0 \
    PORT=8080

COPY --from=build /app/publish .

EXPOSE 8080

# Render fournit la variable d'environnement $PORT : l'API doit écouter dessus.
# On utilise la forme "shell" pour que $PORT soit interprété au démarrage du conteneur.
ENTRYPOINT ["sh", "-c", "dotnet api-gestion.dll --urls http://0.0.0.0:${PORT:-8080}"]
