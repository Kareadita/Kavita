#This Dockerfile creates a build for all architectures

#Image that copies in the files and passes them to the main image
FROM ubuntu:focal AS copytask

ARG TARGETPLATFORM

#Move the output files to where they need to be
RUN mkdir /files
COPY _output/*.tar.gz /files/
COPY UI/Web/dist /files/wwwroot
COPY copy_runtime.sh /copy_runtime.sh
RUN /copy_runtime.sh

#Production image
FROM ubuntu:focal

COPY --from=copytask /Kavita /kavita
COPY --from=copytask /files/wwwroot /kavita/wwwroot

#Installs program dependencies
RUN apt-get update \
  && apt-get install -y libicu-dev libssl1.1 libgdiplus \
  && rm -rf /var/lib/apt/lists/*

COPY entrypoint.sh /entrypoint.sh

EXPOSE 5000

WORKDIR /kavita

ENTRYPOINT [ "/bin/bash" ]
CMD ["/entrypoint.sh"]
