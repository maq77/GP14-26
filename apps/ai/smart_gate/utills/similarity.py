# utils/similarity.py
import numpy as np
from scipy.spatial.distance import cosine

def cosine_similarity(emb1, emb2):
    """Compute cosine similarity between two embeddings"""
    emb1 = emb1 / np.linalg.norm(emb1)
    emb2 = emb2 / np.linalg.norm(emb2)
    return (emb1 * emb2).sum()

def euclidean_distance(emb1, emb2):
    """Compute Euclidean distance between two embeddings"""
    return np.linalg.norm(emb1 - emb2)

def find_best_match(embedding, database, threshold=0.6):
    """
    Compare embedding to all entries in database.
    Return (name, score) of the best match or None if no match.
    """
    best_score = -1
    best_name = None
    for name, db_emb in database.items():
        score = cosine_similarity(embedding, db_emb)
        if score > best_score and score > threshold:
            best_score = score
            best_name = name
    return best_name, best_score
