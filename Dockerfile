FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends openssh-client git \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /work
RUN git clone https://github.com/KaenguruuDev/SenfCli.git /work/cli

CMD ["sleep", "infinity"]