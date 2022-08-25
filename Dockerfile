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
  && apt-get install -y libicu-dev libssl1.1 libgdiplus curl \
  && apt-get install -y libvips --no-install-recommends \
  && rm -rf /var/lib/apt/lists/*

#Removes the libvips.so.42 file to fix the AVX CPU requirement issue
RUN rm /kavita/libvips.so.42

COPY entrypoint.sh /entrypoint.sh

EXPOSE 5000

WORKDIR /kavita

HEALTHCHECK --interval=30s --timeout=15s --start-period=30s --retries=3 CMD curl --fail http://localhost:5000 || exit 1

ENTRYPOINT [ "/bin/bash" ]
CMD ["/entrypoint.sh"]
