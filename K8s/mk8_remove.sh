microk8s kubectl delete -f ./musicdata-pgsql-pv-pvc.yaml
microk8s kubectl delete -f ./musicdata-pgsql-depl.yaml
microk8s kubectl delete -f ./musicdata-pgsql-svc.yaml

microk8s kubectl delete -f ./musicdata-api-depl.yaml
microk8s kubectl delete -f ./musicdata-api-svc.yaml