microk8s kubectl delete -f ./auth-pgsql-depl.yaml
microk8s kubectl delete -f ./auth-pgsql-svc.yaml
microk8s kubectl delete -f ./auth-pgsql-pv-pvc.yaml

microk8s kubectl delete -f ./auth-api-depl.yaml
microk8s kubectl delete -f ./auth-api-svc.yaml

microk8s kubectl delete -f ./musicdata-pgsql-depl.yaml
microk8s kubectl delete -f ./musicdata-pgsql-svc.yaml

microk8s kubectl delete -f ./musicdata-api-depl.yaml
microk8s kubectl delete -f ./musicdata-api-svc.yaml
microk8s kubectl delete -f ./musicdata-pgsql-pv-pvc.yaml

microk8s kubectl delete -f ./playlist-pgsql-pv-pvc.yaml
microk8s kubectl delete -f ./playlist-pgsql-depl.yaml
microk8s kubectl delete -f ./playlist-pgsql-svc.yaml

microk8s kubectl delete -f ./playlist-api-depl.yaml
microk8s kubectl delete -f ./playlist-api-svc.yaml