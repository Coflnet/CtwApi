VERSION=0.0.1

rm -rf out

docker run --rm -v "${PWD}:/local" --network host -u $(id -u ${USER}):$(id -g ${USER})  openapitools/openapi-generator-cli generate \
-i http://localhost:5122/api/openapi/v1/openapi.json \
-g csharp \
-o /local/out --additional-properties=packageName=Coflnet.Ctw.Api.Client,packageVersion=$VERSION,licenseId=MIT,targetFramework=net6.0

rm -rf out/src/Coflnet.Ctw.Api.Client.Test
rm -rf out/Coflnet.Ctw.Api.Client.sln