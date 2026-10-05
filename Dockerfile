#This Dockerfile creates a build for all architectures

FROM ubuntu:noble AS copytask

ARG TARGETPLATFORM

#Move the output files to where they need to be
RUN mkdir /files
COPY _output/*.tar.gz /files/

RUN set -eux; \
    case "$TARGETPLATFORM" in \
      "linux/amd64")   RID=linux-x64   ;; \
      "linux/arm64")   RID=linux-arm64 ;; \
      "linux/arm/v7")  RID=linux-arm   ;; \
      *) echo "Unsupported platform: $TARGETPLATFORM" >&2; exit 1 ;; \
    esac; \
    tar xzf "/files/kavita-${RID}.tar.gz" -C /

#Production image
FROM ubuntu:noble

#Installs program dependencies
ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
  && apt-get install -y libicu-dev libgdiplus curl tzdata libjemalloc2 \
  && rm -rf /var/lib/apt/lists/*

COPY --from=copytask /Kavita /kavita
COPY Kavita.Server/config/appsettings.json /tmp/config/appsettings.json

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 5000

WORKDIR /kavita

HEALTHCHECK --interval=30s --timeout=15s --start-period=30s --retries=3 CMD curl -fsS http://localhost:5000/api/health || exit 1

# Enable detection of running in a container
ENV DOTNET_RUNNING_IN_CONTAINER=true
# Set the time zone
ENV TZ=UTC

ENTRYPOINT [ "/bin/bash" ]
CMD ["/entrypoint.sh"]
