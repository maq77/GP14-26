from fastapi import FastAPI
app = FastAPI()

@app.get("/health")
def health():
    return {"status":"ok"}

# TODO: add /infer/detect and /infer/face/verify (for REST testing)
