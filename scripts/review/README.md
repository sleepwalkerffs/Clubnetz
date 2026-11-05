# Review environments

Every pull request from a branch of this repository gets its own copy of the app at
`https://pr-<number>.review.clubnetz.app`, so you can click through a change before merging it.

- **Access:** a username and password prompt in front of everything (`REVIEW_BASIC_AUTH`). Behind it, log in
  to the app with your own email and the review password (`REVIEW_LOGIN_PASSWORD`, never your real one).
  Every other account has the review password too, its email is `<first name>.<last name>.<user id>@example.test`
  (see the member list as an admin).
- **Data:** a pseudonymized copy of production (`pseudonymize.sql`). Names, email addresses, addresses,
  phone numbers, birthdays, profile pictures, comments, notes, guest names, announcement and event texts,
  court blocking titles and email templates are replaced or removed, clubs and courts renamed. SumUp tokens,
  Stripe ids, club API keys, push subscriptions and login tokens are removed. Bookings, purchases, families,
  badges and club configuration stay as they are, so insiders could still recognise people by their
  booking history: treat it as personal data, not as anonymous.
- **Emails** never leave the server, they land in the mail catcher at `https://mail.review.clubnetz.app`.
  Push notifications and payments (Stripe, SumUp) are switched off.
- **Lifecycle:** opening a pull request creates the environment with a fresh copy of the template database,
  every push updates the app (the database is kept, migrations run at startup), closing the pull request
  removes it. Reopen a pull request to start over with fresh data.
- At most 3 environments run at the same time (`REVIEW_MAX_ENVIRONMENTS`), the server also runs production.
- Pull requests from forks and from Dependabot never get one. Pull requests with merge conflicts don't get
  updated (GitHub runs no workflow for them), and neither do pushes made with a workflow's `GITHUB_TOKEN`.

## How it works

```
GitHub workflow (.github/workflows/review-environment.yml)
  builds ghcr.io/sleepwalkerffs/clubnetz-review:<commit>  (a package of its own, not the production image)
  ssh clubnetz-review@server "deploy <pr> <commit>"   (or "remove <pr>" when the PR is closed)
        |
        v
ssh-entry.sh     the only command the workflow's key may run, accepts nothing but those two commands
        | sudo (rule for this one script)
        v
review-env.sh    creates the role and database pr_<n> from review_template and runs the container
                 clubnetz-pr-<n> with Traefik labels (password prompt, noindex). One script at a time (flock).

internal network clubnetz-review:  clubnetz-review-postgres, clubnetz-review-mailpit, clubnetz-pr-<n>, Coolify's proxy
```

- The network is `--internal`: the review containers have no route to the internet, the host or Coolify's
  networks (production database, Coolify), only the proxy can reach them.
- Each review environment connects with its own database role (new password on every deployment) that owns
  only its database and cannot connect to any other.
- The production data never touches the review database server: `refresh-template.sh` pipes `pg_dump`
  into a throwaway PostgreSQL container without network whose data lives in memory, pseudonymizes it
  there and only copies the result over. So the review databases don't hold replaced values, not even as
  old row versions on disk. The throwaway container is removed afterwards, also when something fails.

## One-time setup

### 1. DNS (Squarespace)

In the DNS settings of `clubnetz.app` add an `A` record with host `*.review` pointing at `2.31.24.78`.
Check it after a few minutes with `nslookup pr-1.review.clubnetz.app`.

### 2. Server (as root)

```bash
git clone https://github.com/sleepwalkerffs/Clubnetz.git /opt/clubnetz
mkdir -p /etc/clubnetz-review
cp /opt/clubnetz/scripts/review/review.env.example /etc/clubnetz-review/review.env
chmod 600 /etc/clubnetz-review/review.env
nano /etc/clubnetz-review/review.env
```

Fill in:

- `REVIEW_BASIC_AUTH`: output of `docker run --rm httpd:2.4-alpine htpasswd -nbB <user> '<password>'`, in single quotes.
- `REVIEW_LOGIN_PASSWORD`: the password of all accounts in the review environments. Use one you use nowhere else.
- `REVIEW_POSTGRES_PASSWORD`: output of `openssl rand -hex 24`.
- `PRODUCTION_DB_CONTAINER`, `PRODUCTION_DB_NAME`, `PRODUCTION_DB_USER`: the production database in Coolify.
  `docker ps --format '{{.Names}} {{.Image}}' | grep postgres` lists the containers; the database name
  and user are in the database's settings in Coolify.
- Check the Traefik names: `docker inspect coolify-proxy | grep -E 'entrypoints|certresolver'` should show
  the entry points `http` and `https` and the resolver `letsencrypt`. Adjust if not. Also check that the
  proxy's dashboard is not open without a password (`api.insecure`), the review containers can reach the proxy.

Let root pull the review image (it is created as a private package on the first pull request): run
`docker login ghcr.io -u sleepwalkerffs` with a GitHub token (classic) that has only `read:packages`.

Create the key for the workflow and set everything up:

```bash
ssh-keygen -t ed25519 -N "" -C github-review-environments -f /root/clubnetz-review-key
/opt/clubnetz/scripts/review/setup-server.sh /root/clubnetz-review-key.pub
tmux new -s refresh   # so a lost SSH connection does not stop the refresh halfway
/opt/clubnetz/scripts/review/refresh-template.sh <your email in production>
```

### 3. GitHub

In the repository settings → Environments, create the environment `Review` with:

- secret `REVIEW_SSH_KEY`: the content of `/root/clubnetz-review-key` (the private key). Delete it from the
  server afterwards: `shred -u /root/clubnetz-review-key`.
- secret `REVIEW_SSH_KNOWN_HOSTS`: output of `ssh-keyscan 2.31.24.78`.
- variable `REVIEW_SSH_HOST`: `2.31.24.78`.

Then open a pull request. When the workflow is done, the pull request shows "View deployment".

## Maintenance

| What | How (as root on the server) |
| --- | --- |
| Fresher data | `/opt/clubnetz/scripts/review/refresh-template.sh <your email>` in tmux (new environments use it, reopen a PR to reset one) |
| See what runs | `/opt/clubnetz/scripts/review/review-env.sh list` |
| Remove one by hand | `/opt/clubnetz/scripts/review/review-env.sh remove <pr>` |
| App logs | `docker logs clubnetz-pr-<n>` (deliberately not in the public workflow log) |
| New version of these scripts | `cd /opt/clubnetz && git pull`, then run `setup-server.sh` again |
| Change the prompt password | edit `REVIEW_BASIC_AUTH`, run `setup-server.sh` again, then push to open PRs (or reopen them) |
| Change the review password | edit `REVIEW_LOGIN_PASSWORD`, run `refresh-template.sh` again |

When a migration adds a column that can hold personal data, free text or a secret, add it to `pseudonymize.sql`.

## Good to know

- Each review hostname gets its own Let's Encrypt certificate, so `pr-<n>.review.clubnetz.app` shows up in
  public certificate logs. That reveals that review environments exist, nothing more.
- The review images stay in the `clubnetz-review` package after a pull request is closed. Delete old ones in
  the package settings on GitHub now and then.
- After closing a pull request, GitHub still shows its last deployment; the link no longer works.
