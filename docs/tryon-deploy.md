# Virtual try-on: deploy and test on a phone

Document only. Nothing here has been deployed.

## Fastest: everything from your phone (no laptop)

Needs the `tryon-feature` branch on GitHub, a free Vercel account, and (for step B) a free Render account. Sign in to both with GitHub.

A. Customer try-on only, 6 built-in samples (about 5 minutes):

1. On the phone, open vercel.com, Add New, Project, import `nbjewel`.
2. Branch: `tryon-feature`. Root Directory: `frontend`. Framework preset: Other. Leave `NBJEWEL_API_URL` empty.
3. Deploy. Open `https://<your-app>.vercel.app/tryon`, tap a piece, allow the camera.

Without the API the app runs in sample mode: the try-on works with the 6 samples, and the owner page says uploads are off.

B. Add the API for the owner page (uploads, Active toggle, delete):

1. On the phone, open dashboard.render.com, New, Blueprint, pick the repo and branch `tryon-feature`. Render reads `render.yaml` and builds `backend/Dockerfile`.
2. When asked, set `OWNER_PIN` (8 or more characters) and `CORS_ORIGINS` = `https://<your-app>.vercel.app`.
3. Wait for the deploy (the first build takes a few minutes). Check `https://<your-api>.onrender.com/api/jewelry` lists 6 items.
4. In Vercel, Settings, Environment Variables, set `NBJEWEL_API_URL` = `https://<your-api>.onrender.com`, then Deployments, Redeploy.
5. Open `https://<your-app>.vercel.app/owner/jewelry`, enter the PIN.

Render free plan: the API sleeps after about 15 minutes idle (the first call then takes up to a minute) and its disk is temporary, so uploads are lost on restart.

## Full detail

Phone browsers only allow the camera on `https` pages (or `localhost`). So both parts must be on https:

1. The Angular app on a static host (Vercel or Netlify).
2. The .NET API on a host that runs .NET 10 (Azure App Service is used below; any .NET or Docker host works).

The app calls the API from the browser, so the API must allow the app's address in CORS.

## 1. Deploy the .NET API (Azure App Service, Linux)

1. Create a Web App: Runtime stack `.NET 10`, Linux, the free or basic plan.
2. Publish from the repo root:

   ```bash
   dotnet publish backend/NbJewel.Api -c Release -o publish
   ```

   Then deploy the `publish` folder (VS Code Azure extension, `az webapp deploy`, or a zip deploy). The seed images are inside `publish/sample-data/jewelry`.
3. In the Web App, Settings, Environment variables, add:

   | Name | Value |
   |---|---|
   | `OWNER_PIN` | a PIN only the shop owner knows, 8 or more characters with letters and digits |
   | `CORS_ORIGINS` | the app address from step 2, for example `https://nbjewel-tryon.vercel.app` (comma separated if more than one, no trailing slash) |
   | `JEWELRY_STORAGE_PATH` | `/home/data/jewelry` (on Azure App Service Linux, `/home` survives restarts) |
   | `GEMINI_API_KEY`, `GEMINI_MODEL` | only if the chatbot should work too |

4. Check: open `https://<your-api>/api/jewelry`. It should list 6 sample items.

Other hosts. Render, Railway or Fly.io need a Docker image for .NET. A minimal Dockerfile (not in the repo):

```
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish backend/NbJewel.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NbJewel.Api.dll"]
```

Set the same environment variables there. On those hosts, attach a persistent disk and point `JEWELRY_STORAGE_PATH` at it, or uploads are lost (see Known limit below).

## 2. Deploy the Angular app (Vercel or Netlify)

The repo has `frontend/vercel.json` and `frontend/netlify.toml`. Both run `npm run build:deploy`, which writes the API address from `NBJEWEL_API_URL` into the build, and both send every path to `index.html` so links like `/tryon/abc` work.

Vercel:

1. New Project, import the GitHub repo.
2. Root Directory: `frontend`. Framework preset: Other (the settings come from `vercel.json`).
3. Environment variable: `NBJEWEL_API_URL` = `https://<your-api>` (no trailing slash).
4. Deploy. Note the address, for example `https://nbjewel-tryon.vercel.app`.

Netlify:

1. Add new site, import the repo.
2. Base directory: `frontend` (build command and publish folder come from `netlify.toml`).
3. Environment variable: `NBJEWEL_API_URL` = `https://<your-api>`.
4. Deploy.

Then go back to the API and make sure `CORS_ORIGINS` has exactly this app address. Restart the API after changing it.

## 3. Test on a phone

1. On a computer, open `https://<your-app>/owner/jewelry` and enter the PIN. The page shows the customer link and a QR code.
2. Scan the QR code with the phone camera, or open `https://<your-app>/tryon` in Chrome (Android) or Safari (iPhone).
3. Tap a piece. Allow the camera when asked.
4. Earrings and necklaces: hold the phone at arm's length, face in the frame, good light from the front. Turn your head left and right: the earring on the far side should hide.
5. Rings: show the back of your hand with the fingers up, about 30 cm from the camera. Tap the flip button to use the back camera on someone else's hand.
6. Tap the round shutter button, then "Save photo" (or "Share" on phones that support it).
7. Upload test: on the owner page, upload a PNG or JPG, then reload the customer page on the phone.

If the camera was blocked by mistake: Chrome Android, tap the lock icon in the address bar, Permissions, Camera. iPhone Safari, the "aA" menu, Website Settings, Camera.

## Local test on a phone (optional, no deploy)

The camera needs https, and `http://<computer-ip>:4200` is not https, so a phone on the same Wi-Fi will show "The camera only works on a secure (https) link". Use a tunnel that gives an https address (for example Cloudflare Tunnel or ngrok) for both the app and the API, set `CORS_ORIGINS` to the app tunnel address, and build the app with `NBJEWEL_API_URL` set to the API tunnel address.

## Known limit: uploads on temporary disks

The API stores jewelry metadata (`jewelry.json`) and images on its own disk under `JEWELRY_STORAGE_PATH`. Many free hosts (Render free, Railway without a volume, container restarts in general) give a temporary disk, so owner uploads are lost when the API restarts or redeploys. The 6 samples come back because they are seeded on first start. Fix later by moving to SQL plus blob storage behind `IJewelryRepository`.

## Checklist

- [ ] API `https://<your-api>/api/jewelry` returns 6 items
- [ ] `OWNER_PIN`, `CORS_ORIGINS`, `JEWELRY_STORAGE_PATH` set on the API host
- [ ] `NBJEWEL_API_URL` set on Vercel or Netlify
- [ ] `https://<your-app>/tryon` shows the grid on the phone
- [ ] Camera opens, overlay follows the face or hand, snapshot saves
