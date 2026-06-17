# Veya — project website

A single-page, fully static marketing site for [Veya](https://github.com/s3ba-b/veya).
No build step, no dependencies, no JavaScript framework.

## Files

- `index.html` — the page
- `styles.css` — styling
- `favicon.svg` — logo / favicon
- `og-image.png` — 1200×630 social preview image
- `demos/` — product screenshots (WebP) used in the demo carousel

## Preview locally

Open `index.html` directly in a browser, or serve the folder:

```sh
python3 -m http.server 8000
# then open http://localhost:8000
```

## Deploy

It's plain static files — drop the folder on any host:

- **GitHub Pages** — push to a repo, enable Pages on the branch root.
- **Netlify / Cloudflare Pages / Vercel** — point at this folder, no build command.
- **Any web server** — copy the files into the web root.

## Customize before launch

- Confirm the GitHub URL (`https://github.com/s3ba-b/veya`) in `index.html`.
- Live at [veya-project.org](https://veya-project.org/) (see `CNAME`). The canonical
  link, `og:url`, and `og:image` in `index.html` all point at that domain — update
  them together if the domain ever changes.
