#This Dockerfile pulls the latest git commit and builds Kavita from source
FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS builder

ENV DEBIAN_FRONTEND=noninteractive
ARG TARGETPLATFORM

#Installs nodejs and npm
RUN curl -fsSL https://deb.nodesource.com/setup_14.x | bash - \
  && apt-get install -y nodejs \
  && rm -rf /var/lib/apt/lists/*

#Builds app based on platform
COPY build_target.sh /build_target.sh
RUN /build_target.sh

#Production image
FROM ubuntu:focal

#Move the output files to where they need to be
COPY --from=builder /Projects/Kavita/_output/build/Kavita /kavita

#Installs program dependencies
RUN apt-get update \
  && apt-get install -y libicu-dev libssl1.1 pwgen \
  && rm -rf /var/lib/apt/lists/*

#Creates the manga storage directory
RUN mkdir /manga /kavita/data

RUN cp /kavita/appsettings.Development.json /kavita/appsettings.json \
  && sed -i 's/Data source=kavita.db/Data source=data\/kavita.db/g' /kavita/appsettings.json

COPY entrypoint.sh /entrypoint.sh

EXPOSE 5000

WORKDIR /kavita

ENTRYPOINT ["/bin/bash"]
CMD ["/entrypoint.sh"]