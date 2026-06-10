FROM ubuntu:22.04
RUN apt-get update && apt-get install -y \
    libglib2.0-0 libstdc++6 libgcc-s1 \
    libx11-6 libxcursor1 libxrandr2 libxi6 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY . .
RUN chmod +x ./MetaverseServer
EXPOSE 25000
EXPOSE 25001/udp
CMD ["./MetaverseServer", "-batchmode", "-nographics", "-logFile", "-"]
