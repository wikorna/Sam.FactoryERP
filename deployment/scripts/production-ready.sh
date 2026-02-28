# 1) Load image from tar
docker load -i ane-api-staging.tar

# 2) Verify image exists
docker images | grep -E '^ane-api\s+staging'

# (Optional but recommended) 2.1) Inspect image labels / created / size
docker image inspect ane-api:staging --format \
  'ID={{.Id}} Created={{.Created}} Size={{.Size}}'

# 3) Deploy
sudo ./deploy-staging.sh

# 4) Post-deploy checks (recommended)
docker ps --filter "name=ane-api-staging" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
docker logs --tail 100 ane-api-staging

# If you have health endpoint:
curl -k https://localhost:5001/health || curl http://localhost:5000/health
