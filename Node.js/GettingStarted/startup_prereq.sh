cd $(dirname $0)
sudo apt-get update | sudo apt-get upgrade
sudo apt-get -y install python-pip
sudo apt-get -y install git
sudo apt-get -y install python-dev
sudo apt-get -y install build-essential
sudo apt-get -y install libssl-dev
sudo apt-get -y install libffi-dev
sudo pip install -U pyOpenSSL
sudo pip install cryptography && echo 'y'
sudo pip install azure && echo 'y'
sudo pip install azure-storage --upgrade && echo 'y'