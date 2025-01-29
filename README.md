# FileServerPOC
POC







For AWS CLI SSO Setup ---------------------------------

    dotnet add package AWSSDK.SSO --version 3.7.100.13
    dotnet add package AWSSDK.SSOOIDC
    aws configure sso


For SNS setup ----------------------------------------
    1. Create SNS Topic
    2. Edit Access Policy, Set the following
        a. SNS URN
        b. Account ID
        c. S3 Bucket URN

        {
          "Version": "2012-10-17",
          "Id": "example-ID",
          "Statement": [
            {
              "Sid": "Example SNS topic policy",
              "Effect": "Allow",
              "Principal": {
                "Service": "s3.amazonaws.com"
              },
              "Action": "SNS:Publish",
              "Resource": " URN of SNS",
              "Condition": {
                "StringEquals": {
                  "aws:SourceAccount": "Account ID"
                },
                "ArnLike": {
                  "aws:SourceArn": "URN of S3 Bucket"
                }
              }
            }
          ]
        }


3. Go to S3 Bucket Properties
4. Create Event Notification and Select the newly SNS Topic as notification endpoint            
5. Come to SNS dashboard under subscription setup Email and Lambda Fuction
6. paste Lambda Function URN


For Lambda Setup ----------------------

1. Create a Lambda function (Which will be used in SNS)
2. Upload Handler.zip as code
3. Deploy and Test (verify logs)

For RDS Setup ---------------------------

1. Create a new Database Instance in AWS RDS ( we used MySQL Freetier)                            
2. set ConnectionString in appsettings.json 
    "ConnectionStrings": {
        "DefaultConnection": "Server=file-server-db.cx644oceaozv.eu-north-1.rds.amazonaws.com,3306;Database=fileServerDB;User Id=your-user;Password=your-password;"
     }
3. 
