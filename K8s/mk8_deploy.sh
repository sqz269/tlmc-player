microk8s kubectl apply -f ./auth-pgsql-pv-pvc.yaml
microk8s kubectl apply -f ./auth-pgsql-depl.yaml
microk8s kubectl apply -f ./auth-pgsql-svc.yaml

microk8s kubectl apply -f ./auth-api-depl.yaml
microk8s kubectl apply -f ./auth-api-svc.yaml

microk8s kubectl apply -f ./musicdata-pgsql-pv-pvc.yaml
microk8s kubectl apply -f ./musicdata-pgsql-depl.yaml
microk8s kubectl apply -f ./musicdata-pgsql-svc.yaml

microk8s kubectl apply -f ./musicdata-api-depl.yaml
microk8s kubectl apply -f ./musicdata-api-svc.yaml

microk8s kubectl apply -f ./playlist-pgsql-pv-pvc.yaml
microk8s kubectl apply -f ./playlist-pgsql-depl.yaml
microk8s kubectl apply -f ./playlist-pgsql-svc.yaml

microk8s kubectl apply -f ./playlist-api-depl.yaml
microk8s kubectl apply -f ./playlist-api-svc.yaml