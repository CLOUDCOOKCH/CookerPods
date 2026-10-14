# Static HTTPS hosting

Publish only `index.html`, `styles.css`, `probe.js`, and `app.js` together, without rewriting their paths. There is no build step or server-side runtime.

## GitHub Pages

The repository includes `.github/workflows/pages.yml`. A push to `main`, or a manual run in **Actions → Publish browser probe to GitHub Pages**, runs the unit and syntax checks, stages only the four application files and license, and deploys the static artifact using the official GitHub Pages actions. The actions are pinned to verified commit SHAs.

First enable **Settings → Pages → Build and deployment → Source → GitHub Actions**. The workflow's default GitHub token can deploy an enabled site but cannot automatically create a Pages site through `configure-pages`. If an earlier run failed before this setting was enabled, rerun it from the Actions tab. No personal access token needs to be added to the workflow. Repository/organization policy or a private repository's GitHub plan may restrict Pages. GitHub supplies the deployed HTTPS address in the workflow's `github-pages` environment; a successful push alone does not prove publication.

To remove the website, unpublish it in **Settings → Pages**. An earlier code version can be redeployed by reverting the corresponding source change on `main`. HTTPS hosting is a prerequisite for the supported hardware test, not installation on the visitor's PC.

## Security headers

The page includes a restrictive Content Security Policy meta tag and no-referrer policy. On hosts with configurable response headers, also set:

```http
Content-Security-Policy: default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'none'; base-uri 'none'; form-action 'none'; object-src 'none'; frame-ancestors 'none'
Permissions-Policy: bluetooth=(self), serial=(self)
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
```

`frame-ancestors` must be a response header; a meta tag cannot provide it. GitHub Pages does not provide arbitrary custom response-header configuration. Prefer top-level browsing; embedding can deny device permissions. Verify actual host headers and browser behavior rather than treating this example as deployed configuration. Bluetooth reads do not use `fetch`; `connect-src 'none'` deliberately prevents network uploads by this code.
