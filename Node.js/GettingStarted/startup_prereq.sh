cd $(dirname $0)
sudo apt-get update && echo 'y' | sudo apt-get upgrade
sudo apt-get -y install python-pip && echo 'y'
sudo apt-get -y install git && echo 'y'
sudo apt-get -y install python-dev && echo 'y'
sudo apt-get -y install build-essential && echo 'y'
sudo apt-get -y install libssl-dev
sudo apt-get -y install libffi-dev
sudo apt-get -y install python-dev
sudo pip install -U pyOpenSSL
sudo pip install cryptography && echo 'y'
sudo pip install azure && echo 'y'
sudo pip install azure-storage --upgrade && echo 'y'
#sudo pip install azure-storage && echo 'y'
#sudo git clone git://github.com/Azure/azure-storage-python.git /home/azure-storage-python
#cd /home/azure-storage-python
#sudo python setup.py install