#This Dockerfile creates a build for all architectures

#Image that copies in the files and passes them to the main image
FROM ubuntu:noble AS copytask

ARG TARGETPLATFORM

#Move the output files to where they need to be
RUN mkdir /files
COPY _output/*.tar.gz /files/
COPY UI/Web/dist/browser /files/wwwroot
COPY copy_runtime.sh /copy_runtime.sh

RUN chmod +x /copy_runtime.sh
RUN /copy_runtime.sh
RUN chmod +x /Kavita/Kavita

#Production image
FROM ubuntu:noble

COPY --from=copytask /Kavita /kavita
COPY --from=copytask /files/wwwroot /kavita/wwwroot
COPY API/config/appsettings.json /tmp/config/appsettings.json

#Installs program dependencies
RUN apt-get update \
  && apt-get install -y libicu-dev libgdiplus curl \
  && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 5000

WORKDIR /kavita

HEALTHCHECK --interval=30s --timeout=15s --start-period=30s --retries=3 CMD curl -fsS http://localhost:5000/api/health || exit 1

# Enable detection of running in a container
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT [ "/bin/bash" ]
CMD ["/entrypoint.sh"]
