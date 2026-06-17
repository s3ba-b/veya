# HOWTO: attach a custom domain (after you buy it)

This site is served by GitHub Pages at
`https://s3ba-b.github.io/veya-website/`. Once you own a domain, point it here.
Free HTTPS is included.

Replace `example.com` below with your real domain.

## 1. Add DNS records at your registrar

Open your registrar's DNS settings (Namecheap, Cloudflare, Porkbun, etc.).

**Apex domain** (`example.com`) — add four `A` records:

```
185.199.108.153
185.199.109.153
185.199.110.153
185.199.111.153
```

Optionally also add the IPv6 `AAAA` records:

```
2606:50c0:8000::153
2606:50c0:8001::153
2606:50c0:8002::153
2606:50c0:8003::153
```

**`www` subdomain** (`www.example.com`) — add one `CNAME` record:

```
www  →  s3ba-b.github.io
```

Recommended: set up **both** the apex (`A` records) and `www` (CNAME), then let
GitHub redirect one to the other (step 3 handles which is primary).

> If your DNS is on Cloudflare, set the records to **DNS only** (grey cloud)
> until GitHub has issued the certificate, then you can re-enable the proxy.

## 2. Set the custom domain in the repo

GitHub UI: **Settings → Pages → Custom domain** → enter `example.com` → **Save**.

This commits a `CNAME` file to the repo. Keep that file in `main` — if it's
removed, the custom domain resets.

CLI equivalent:

```sh
gh api -X PUT repos/s3ba-b/veya-website/pages -f cname=example.com
```

## 3. Wait for the DNS check, then enforce HTTPS

- GitHub runs a DNS check (minutes to a few hours after records propagate).
- Once it passes, GitHub provisions a free Let's Encrypt certificate.
- Then tick **Enforce HTTPS** (Settings → Pages).

## 4. Verify

```sh
dig +short example.com            # should list the four GitHub A record IPs
curl -sI https://example.com | head -1   # expect: HTTP/2 200
```

## Notes

- The deploy workflow (`.github/workflows/deploy.yml`) is unaffected — pushes to
  `main` keep deploying to whatever domain is configured.
- No code change is needed for the domain to work; it's all DNS + the `CNAME`
  file GitHub manages.
- For richer social previews, after the domain is live you can set `og:url` and a
  canonical `<link>` in `index.html` to the new URL.
