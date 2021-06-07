#This Dockerfile creates a build for all architectures

#Production image
FROM ubuntu:focal AS copytask

ARG TARGETPLATFORM

#Move the output files to where they need to be
RUN mkdir /files
COPY _output/*.tar.gz /files/
COPY copy_runtime.sh /copy_runtime.sh
RUN /copy_runtime.sh

FROM ubuntu:focal

COPY --from=copytask /kavita /kavita

#Installs program dependencies
RUN apt-get update \
  && apt-get install -y libicu-dev libssl1.1 pwgen \
  && rm -rf /var/lib/apt/lists/*

#Creates the data directory
RUN mkdir /kavita/data

RUN cp /kavita/appsettings.Development.json /kavita/appsettings.json \
  && sed -i 's/Data source=kavita.db/Data source=data\/kavita.db/g' /kavita/appsettings.json

COPY entrypoint.sh /entrypoint.sh

EXPOSE 5000

WORKDIR /kavita

ENTRYPOINT ["/bin/bash"]
CMD ["/entrypoint.sh"]
