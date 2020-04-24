# Docker configuration of SOS server

This document describes how to use the [52North SOS](https://hub.docker.com/r/52north/sos/) container to hold and serve OGC time-series data.

Requires:
- [Docker Compose](https://docs.docker.com/compose/install/) v1.25+
- [Docker Engine](https://docs.docker.com/install/) v18+
- The contents of the [docker-compose](./docker-compose) folder, which has two files:
  - `docker-compose.yml` to configure the three docker containers
  - `ngix-proxy.conf` to configure the nginx reverse proxy

# Starting/stopping the Docker containers

Start the docker containers using the `docker-compose` command, in the same folder as the `docker-compose.yml` file:

```sh
$ docker-compose up
```

You can stop the containers too, either by typing Ctrl-C, or starting a new console and using the `down` command.

```sh
$ docker-compose down
```

## First-time setup

Before you can feed the SOS server with time-series data, the SOS server must be configured.

Browse to http://localhost/ to load the stock 52North interface:

![Click Here To Start Installation](ClickHereToStartInstallation.png)

Click on the red **here** link to start the configuration sequence.

![Install Start Page](InstallStartPage.png)

Click the **Start** button in the lower right corner

![Start Button](StartButton.png)

Select the "PostgreSQL/PostGIS" option and change the host to "db"

![Select Data Source](SelectDataSource.png)

Click the **Next** button at the bottom of the page.

![Confirm Data Source Step](ConfirmDataSourceStep.png)

Select the **Transactional Security** tab

![Select Transactional Security Tab](SelectTransactionalSecurityTab.png)

Disable the **Transactional security active** checkbox and click **Next**

![Disable Transactional Checkbox](DisableTransactionalCheckbox.png)

Enter an admin username and password and click **Install**

![Finish Install](FinishInstall.png)

Your SOS server configuration is complete.

![Setup Completed](SetupCompleted.png)