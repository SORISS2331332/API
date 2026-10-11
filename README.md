# API — Gestion & Suivi de Stock

API ASP.NET Core (.NET 10) exposant l'authentification, la gestion des produits
(code-barres, étiquettes PDF), le scan, les mouvements de stock et les
statistiques. Base de données PostgreSQL (Supabase).

## Déploiement sur Render (Docker)

Un `Dockerfile` multi-étapes est fourni dans `api-gestion/` :

1. Sur [Render](https://render.com), créer un **Web Service** → **Build and deploy from a Git repository**.
2. **Root Directory** : `api-gestion`
3. **Runtime** : `Docker` (le `Dockerfile` est détecté automatiquement à la racine du dossier choisi).
4. **Variables d'environnement** à définir dans Render (Dashboard → Environment) :
   - `ConnectionStrings__DefaultConnection` : chaîne de connexion PostgreSQL (Supabase).
   - `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__DureeValiditeHeures` (optionnel, sinon valeurs d'`appsettings.json`).
   - `Cors__OrigensAutorisees__0`, `Cors__OrigensAutorisees__1`, ... pour ajouter des origines front-end autorisées sans modifier le code.
   - Render injecte automatiquement `PORT` : le conteneur écoute dessus (voir `ENTRYPOINT` du `Dockerfile`).
5. Les migrations EF Core et le seed des données initiales s'exécutent automatiquement au démarrage du service.
6. Une fois l'API déployée, récupérer son URL Render (ex: `https://api-gestion-stock.onrender.com`) et la renseigner dans les variables d'environnement Vercel des deux front-ends :
   - **Gestion** (`https://gestion-nine-mu.vercel.app`) : `NEXT_PUBLIC_API_URL` et `NEXT_PUBLIC_HUB_URL` (`<url-render>/hubs/stock`).
   - **Scan** (`https://scan-ten-rho.vercel.app`) : `NEXT_PUBLIC_API_URL`.
   - Redéployer les deux projets Vercel après la mise à jour des variables (Next.js embarque les `NEXT_PUBLIC_*` au moment du build).

Les origines des deux front-ends Vercel sont déjà autorisées dans la configuration CORS (`appsettings.json`).
