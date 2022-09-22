microk8s kubectl apply -f ./pgsql-musicdata-svc.yaml
microk8s kubectl apply -f ./pgsql-musicdata-depl.yaml
microk8s kubectl apply -f ./pgsql-musicdata-pv-pvc.yaml

microk8s kubectl apply -f ./musicdata-api-svc.yaml
microk8s kubectl apply -f ./musicdata-api-depl.yaml