#!/usr/bin/env bash

set -e
set -o pipefail

apt-key adv --keyserver hkp://p80.pool.sks-keyservers.net:80 --recv-keys 58118E89F3A912897C070ADBF76221572C52609D
echo deb https://apt.dockerproject.org/repo ubuntu-trusty main > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get purge -y lxc-docker
apt-get install -y docker-engine
service docker stop
echo DOCKER_OPTS=\"-H tcp://127.0.0.1:2375 -H unix:///var/run/docker.sock\" >> /etc/default/docker
rm -f /var/lib/docker/network/files/local-kv.db
service docker start
